using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using OfficeOpenXml;

namespace Terus_Traffic.Ultilities
{
    public sealed class DataSource
    {
        // --- Singleton Implementation ---
        private static readonly Lazy<DataSource> lazyInstance =
            new Lazy<DataSource>(() => new DataSource());
        public static DataSource Instance => lazyInstance.Value;

        // --- Configuration ---
        private readonly string _filePath;
        private readonly string _worksheetName;
        private readonly string _idColumnName;

        // Excel Header Names (Case might matter depending on checks)
        private const string ExcelColId = "ID";
        private const string ExcelColKeyword = "Keyword";
        private const string ExcelColUrl = "URL";
        private const string ExcelColRank = "Rank";
        private const string ExcelColRequireQuantity = "Require Quantity";
        private const string ExcelColCurrentQuantity = "Current Quantity";
        private const string ExcelColType = "Type";
        private const string ExcelColMobile = "Mobile";


        // --- State ---
        private readonly object _fileLock = new object(); // Lock for physical file access
        private readonly object _dataLock = new object(); // Lock for ObservableCollection access synchronization

        // *** Use ObservableCollection<TrafficUrlItem> ***
        public ObservableCollection<TrafficUrlItem> Items { get; private set; }

        // --- Private Constructor ---
        private DataSource()
        {
            // !! IMPORTANT: Configure your settings here !!
            //_filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TrafficData.xlsx");
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            bool? response = openFileDialog.ShowDialog();

            if (response == true)
            {
                string filePath = openFileDialog.FileName;
                MessageBox.Show(filePath);
                _filePath = filePath;
            }
            //_filePath = Path.Combine("D:\\Visual Studio\\projects\\Terus Traffic\\Data\\TrafficData.xlsx");
            Console.WriteLine("Data Source: " + _filePath);
            _worksheetName = "Traffic URLs"; // <<<--- SET Your Sheet Name
            _idColumnName = nameof(TrafficUrlItem.Id); // <<<--- SET C# Property Name for ID (usually "Id")

            ExcelPackage.License.SetNonCommercialPersonal("Traffic");

            Items = new ObservableCollection<TrafficUrlItem>();
            BindingOperations.EnableCollectionSynchronization(Items, _dataLock);
        }

        // --- Initialization (Call this from WPF App Startup) ---
        public async Task<bool> InitializeAsync()
        {
            bool fileReady = await EnsureDataFileExistsAsync();
            if (fileReady)
            {
                // Attempt to load data even if EnsureDataFile had minor issues
                return await LoadDataFromFileAsync();
            }
            System.Diagnostics.Debug.WriteLine("Initialization failed: Could not ensure data file exists.");
            return false;
        }

        // --- File Handling & Loading (Async) ---
        private Task<bool> EnsureDataFileExistsAsync()
        {
            return Task.Run(() =>
            {
                lock (_fileLock)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        if (!File.Exists(_filePath))
                        {
                            using (var package = new ExcelPackage(new FileInfo(_filePath)))
                            {
                                var worksheet = package.Workbook.Worksheets.Add(_worksheetName);
                                // Create headers based on the specified column names
                                worksheet.Cells["A1"].Value = ExcelColId;
                                worksheet.Cells["B1"].Value = ExcelColKeyword;
                                worksheet.Cells["C1"].Value = ExcelColUrl;
                                worksheet.Cells["D1"].Value = ExcelColRank;
                                worksheet.Cells["E1"].Value = ExcelColRequireQuantity;
                                worksheet.Cells["F1"].Value = ExcelColCurrentQuantity;
                                worksheet.Cells["G1"].Value = ExcelColType;
                                worksheet.Cells["H1"].Value = ExcelColMobile;
                                package.Save();
                                System.Diagnostics.Debug.WriteLine($"Created new Excel file with headers at '{_filePath}'.");
                            }
                        }
                        return true; // File exists or was created
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error ensuring data file exists: {ex.Message}");
                        return false; // Indicate failure
                    }
                }
            });
        }

        private async Task<bool> LoadDataFromFileAsync()
        {
            return await Task.Run(() =>
            {
                lock (_fileLock)
                {
                    List<TrafficUrlItem> loadedItems = new List<TrafficUrlItem>();
                    try
                    {
                        FileInfo fileInfo = new FileInfo(_filePath);
                        if (!fileInfo.Exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"Load Error: File not found at '{_filePath}'.");
                            return false;
                        }

                        using (var package = new ExcelPackage(fileInfo))
                        {
                            var worksheet = package.Workbook.Worksheets[_worksheetName];
                            if (worksheet == null || worksheet.Dimension == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Load Warning: Worksheet '{_worksheetName}' not found or empty.");
                                // Ensure collection is empty if load fails here
                                lock (_dataLock) { Items.Clear(); }
                                return true; // Nothing to load is not a critical failure here
                            }

                            // Header Mapping (Case-insensitive for robustness)
                            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            int maxCol = worksheet.Dimension.End.Column;
                            for (int col = 1; col <= maxCol; col++)
                            {
                                string headerText = worksheet.Cells[1, col].Text?.Trim();
                                if (!string.IsNullOrEmpty(headerText) && !headers.ContainsKey(headerText))
                                {
                                    headers.Add(headerText, col);
                                }
                                else if (!string.IsNullOrEmpty(headerText))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Load Warning: Duplicate header '{headerText}' found at column {col}. Using first occurrence.");
                                }
                            }

                            // Check if essential ID header exists (using const for comparison)
                            if (!headers.ContainsKey(ExcelColId))
                            {
                                System.Diagnostics.Debug.WriteLine($"Load Error: Required ID column header '{ExcelColId}' not found in Excel sheet '{_worksheetName}'.");
                                lock (_dataLock) { Items.Clear(); } // Clear data if essential header missing
                                return false; // Critical error
                            }

                            // Read data rows
                            int endRow = worksheet.Dimension.End.Row;
                            for (int rowNum = 2; rowNum <= endRow; rowNum++)
                            {
                                try
                                {
                                    var item = new TrafficUrlItem
                                    {
                                        // Populate item properties using headers and GetValueFromCell
                                        // Pass the EXCEL column name constants to the helper
                                        Id = GetValueFromCell<int>(worksheet, rowNum, headers, ExcelColId),
                                        Keyword = GetValueFromCell<string>(worksheet, rowNum, headers, ExcelColKeyword),
                                        Url = GetValueFromCell<string>(worksheet, rowNum, headers, ExcelColUrl),
                                        Rank = GetValueFromCell<int>(worksheet, rowNum, headers, ExcelColRank),
                                        RequireQuantity = GetValueFromCell<int>(worksheet, rowNum, headers, ExcelColRequireQuantity),
                                        CurrentQuantity = GetValueFromCell<int>(worksheet, rowNum, headers, ExcelColCurrentQuantity),
                                        Type = GetValueFromCell<string>(worksheet, rowNum, headers, ExcelColType),
                                        Mobile = GetValueFromCell<bool>(worksheet, rowNum, headers, ExcelColMobile)
                                    };

                                    // Basic validation: Skip rows with invalid (0 for int) ID? Adjust if ID can be 0.
                                    if (item.Id <= 0)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Load Warning: Row {rowNum} skipped due to missing or invalid ID ({item.Id}).");
                                        continue;
                                    }

                                    loadedItems.Add(item);
                                }
                                catch (Exception rowEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Load Error: Failed to process row {rowNum}. Error: {rowEx.Message}");
                                }
                            } // End row loop
                        } // End using package

                        // Update ObservableCollection under lock
                        lock (_dataLock)
                        {
                            Items.Clear();
                            foreach (var item in loadedItems) { Items.Add(item); }
                        }
                        System.Diagnostics.Debug.WriteLine($"Data loaded successfully from '{_worksheetName}'. {Items.Count} items.");
                        return true; // Indicate success

                    }
                    catch (IOException ioEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Load Error: File may be locked. {ioEx.Message}");
                        lock (_dataLock) { Items.Clear(); }
                        return false;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Load Error: An unexpected error occurred. {ex.Message}");
                        lock (_dataLock) { Items.Clear(); }
                        return false;
                    }
                } // End fileLock
            }); // End Task.Run
        }

        // Helper to safely get and convert cell value
        private T GetValueFromCell<T>(ExcelWorksheet worksheet, int row, Dictionary<string, int> headers, string excelColumnName)
        {
            // Use case-insensitive lookup for the header name from Excel
            if (!headers.TryGetValue(excelColumnName, out int colIndex))
            {
                // Don't log warning for every cell, maybe just once per load if column is missing entirely?
                // System.Diagnostics.Debug.WriteLine($"Warning: Column '{excelColumnName}' not found in Excel headers for row {row}.");
                return default(T);
            }

            var cellValue = worksheet.Cells[row, colIndex].Value;

            if (cellValue == null || cellValue == DBNull.Value || (cellValue is string s && string.IsNullOrWhiteSpace(s)))
            {
                // Handle specific case for nullable types like int? - return null not default(T) which is 0
                if (typeof(T) == typeof(int?) || typeof(T) == typeof(double?) /* etc. */)
                {
                    return default(T); // which is null for Nullable<T>
                }
                // For non-nullable types where empty means default (e.g., string -> null/empty, int -> 0)
                // Let Convert.ChangeType handle or return default.
                // Returning default is safer.
                return default(T);
            }

            try
            {
                // Handle boolean conversion explicitly for more flexibility (accepts 1/0, TRUE/FALSE)
                if (typeof(T) == typeof(bool))
                {
                    if (cellValue is double d) cellValue = Convert.ToInt32(d); // Handle numbers like 1.0/0.0

                    if (cellValue is int i) return (T)(object)(i != 0); // 1 = true, 0 = false
                    if (cellValue is string str)
                    {
                        return (T)(object)(str.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || str.Equals("1"));
                    }
                    // Fallback or throw? Let ChangeType try below.
                }


                // Attempt general conversion
                return (T)Convert.ChangeType(cellValue, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Conversion Error: Row {row}, Col '{excelColumnName}', Value '{cellValue}', TargetType '{typeof(T).Name}'. {ex.Message}");
                return default(T); // Return default on conversion failure
            }
        }


        public async Task<bool> SaveChangesAsync()
        {
            return await Task.Run(() =>
            {
                lock (_fileLock)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(_filePath);
                        string directory = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) { Directory.CreateDirectory(directory); }

                        using (var package = new ExcelPackage(fileInfo))
                        {
                            var worksheet = package.Workbook.Worksheets[_worksheetName];
                            List<TrafficUrlItem> itemsToSave; // Copy under lock

                            lock (_dataLock) { itemsToSave = new List<TrafficUrlItem>(Items); } // Copy items

                            if (worksheet == null)
                            {
                                worksheet = package.Workbook.Worksheets.Add(_worksheetName);
                                System.Diagnostics.Debug.WriteLine($"Save Warning: Worksheet '{_worksheetName}' created.");
                                // Write headers if sheet is new
                                worksheet.Cells["A1"].Value = ExcelColId;
                                worksheet.Cells["B1"].Value = ExcelColKeyword;
                                worksheet.Cells["C1"].Value = ExcelColUrl;
                                worksheet.Cells["D1"].Value = ExcelColRank;
                                worksheet.Cells["E1"].Value = ExcelColRequireQuantity;
                                worksheet.Cells["F1"].Value = ExcelColCurrentQuantity;
                                worksheet.Cells["G1"].Value = ExcelColType;
                                worksheet.Cells["H1"].Value = ExcelColMobile;
                            }

                            // Clear existing data rows (keep header row 1)
                            if (worksheet.Dimension != null && worksheet.Dimension.End.Row >= 2)
                            {
                                worksheet.DeleteRow(2, worksheet.Dimension.End.Row - 1);
                            }

                            // Write data from the copied list
                            if (itemsToSave.Any())
                            {
                                // --- FIX START: Use Reflection to get MemberInfo[] ---

                                // 1. Define the desired C# property names in the exact order of Excel columns (A, B, C...)
                                string[] propertyNamesInOrder = {
                                     nameof(TrafficUrlItem.Id),
                                     nameof(TrafficUrlItem.Keyword),
                                     nameof(TrafficUrlItem.Url),
                                     nameof(TrafficUrlItem.Rank),
                                     nameof(TrafficUrlItem.RequireQuantity),
                                     nameof(TrafficUrlItem.CurrentQuantity),
                                     nameof(TrafficUrlItem.Type),
                                     nameof(TrafficUrlItem.Mobile)
                                 };

                                // 2. Get the Type of your data model class
                                Type itemType = typeof(TrafficUrlItem);

                                // 3. Get the MemberInfo (PropertyInfo) for each property name in the specified order
                                MemberInfo[] memberInfoOrder = propertyNamesInOrder
                                    .Select(name => itemType.GetProperty(name) as MemberInfo) // Get PropertyInfo, treat as MemberInfo
                                    .Where(mi => mi != null) // Basic check in case a property name is wrong
                                    .ToArray();

                                // 4. Check if we found all members (optional but good practice)
                                if (memberInfoOrder.Length != propertyNamesInOrder.Length)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Save Error: Could not find all specified properties via reflection for type '{itemType.Name}'. Check property names.");
                                    // Handle this error appropriately - perhaps return false or throw
                                    return false;
                                }

                                // 5. Call LoadFromCollection with the MemberInfo array
                                worksheet.Cells["A2"].LoadFromCollection(
                                    itemsToSave,
                                    false, // Don't print headers again
                                    OfficeOpenXml.Table.TableStyles.None,
                                    BindingFlags.Public | BindingFlags.Instance, // Match properties
                                    memberInfoOrder // <<<--- Pass the MemberInfo array here
                                );

                                // --- FIX END ---
                            }

                            // Optional: Auto-fit columns
                            try
                            {
                                if (worksheet.Dimension != null) worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                            }
                            catch (Exception fitEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Warning: Could not auto-fit columns. {fitEx.Message}");
                            }

                            package.Save();
                            System.Diagnostics.Debug.WriteLine($"Data saved successfully to '{_worksheetName}'. {itemsToSave.Count} items.");
                            return true;
                        }
                    }
                    catch (IOException ioEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Save Error: File may be locked. {ioEx.Message}"); return false;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Save Error: An unexpected error occurred. {ex.Message}"); return false;
                    }
                } // end fileLock
            }); // end Task.Run
        }

        // --- CRUD Operations (Work with TrafficUrlItem) ---

        public TrafficUrlItem ReadById(int id) // ID is likely int now based on model
        {
            // If ID is strictly int: if (id == null || !(id is int)) return null; int targetId = (int)id;
            if (id == null) return null; // Keep general object comparison for flexibility

            lock (_dataLock)
            {
                // Equals should work correctly if overridden in TrafficUrlItem based on Id property
                return Items.FirstOrDefault(item => id.Equals(item.Id));
            }
        }

        public bool Add(TrafficUrlItem newItem)
        {
            // Assuming ID is int and cannot be 0 or less, adjust validation if needed
            if (newItem == null || newItem.Id <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"Add Error: Item is null or has invalid ID ({newItem?.Id}).");
                return false;
            }

            lock (_dataLock)
            {
                if (Items.Any(item => newItem.Id.Equals(item.Id)))
                {
                    System.Diagnostics.Debug.WriteLine($"Add Error: Item with ID '{newItem.Id}' already exists.");
                    return false;
                }
                Items.Add(newItem);
                return true;
            }
        }

        public bool Update(TrafficUrlItem updatedItem)
        {
            if (updatedItem == null || updatedItem.Id <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"Update Error: Item is null or has invalid ID ({updatedItem?.Id}).");
                return false;
            }

            lock (_dataLock)
            {
                var existingItem = Items.FirstOrDefault(item => updatedItem.Id.Equals(item.Id));
                if (existingItem != null)
                {
                    // Update properties of the existing item IN the collection
                    existingItem.Keyword = updatedItem.Keyword;
                    existingItem.Url = updatedItem.Url;
                    existingItem.Rank = updatedItem.Rank;
                    existingItem.RequireQuantity = updatedItem.RequireQuantity;
                    existingItem.CurrentQuantity = updatedItem.CurrentQuantity;
                    existingItem.Type = updatedItem.Type;
                    existingItem.Mobile = updatedItem.Mobile;
                    // Do NOT update existingItem.Id
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"Update Error: Item with ID '{updatedItem.Id}' not found.");
                return false;
            }
        }

        public bool Delete(int id) // ID is likely int
        {
            if (id == null) return false;
            // If ID is strictly int: if (!(id is int)) return false; int targetId = (int)id;

            lock (_dataLock)
            {
                var itemToDelete = Items.FirstOrDefault(item => id.Equals(item.Id));
                if (itemToDelete != null)
                {
                    return Items.Remove(itemToDelete); // Remove returns bool
                }
                return false; // Not found
            }
        }
    }
}