using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks; // Used if simple locking needed without async

namespace Terus_Traffic.Ultilities // Change to your actual namespace
{
    public class TrafficClientController
    {
        private readonly DataSource _dataSource;
        private readonly object _lock = new object(); // Lock for controlling access to shared state

        // Tracks the IDs of items currently "checked out" to the UI/client
        private readonly HashSet<object> _checkedOutIds = new HashSet<object>();

        // Remembers the last index checked to distribute items somewhat evenly
        private int _currentIndex = -1;

        public TrafficClientController()
        {
            // Get the singleton DataSource instance
            _dataSource = DataSource.Instance;
            _ = _dataSource.InitializeAsync();
        }

        public async Task Prepare()
        {
            //await DataSource.Instance.InitializeAsync();
        }

        /// <summary>
        /// Gets the next available TrafficUrlItem that needs processing
        /// (where CurrentQuantity is less than RequireQuantity) and is not already checked out.
        /// Loops through the list.
        /// </summary>
        /// <returns>A TrafficUrlItem to process, or null if none are available.</returns>
        public TrafficUrlItem GetNextItemToProcess()
        {
            lock (_lock) // Ensure only one thread modifies state or iterates at a time
            {
                // Check if the underlying data source is ready
                if (_dataSource.Items == null || !_dataSource.Items.Any())
                {
                    System.Diagnostics.Debug.WriteLine("ClientController: DataSource empty or not initialized.");
                    return null;
                }

                int itemCount = _dataSource.Items.Count;
                // Safety break counter in case of infinite loops during high list churn
                int loopSafetyCounter = 0;
                const int maxLoops = 2; // Allow looping through the list twice max per call


                // Start searching from the item *after* the last one returned
                // Make sure _currentIndex is valid before calculating startIndex
                if (_currentIndex < -1 || _currentIndex >= itemCount)
                {
                    _currentIndex = -1; // Reset if index is invalid
                }
                int startIndex = (_currentIndex + 1) % itemCount;


                for (int i = 0; i < itemCount; i++)
                {
                    // Increment loop counter only after checking startIndex once fully
                    if (i == 0 && startIndex != 0) loopSafetyCounter++;
                    if (i > 0 || startIndex == 0) loopSafetyCounter++;


                    if (loopSafetyCounter > itemCount * maxLoops) // Safety break
                    {
                        System.Diagnostics.Debug.WriteLine($"ClientController: GetNextItemToProcess exceeded max attempts ({itemCount * maxLoops}), breaking loop.");
                        break;
                    }


                    int checkIndex = (startIndex + i) % itemCount;


                    // Defensive check in case collection was modified externally despite synchronization
                    if (checkIndex >= _dataSource.Items.Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"ClientController: Index {checkIndex} out of bounds during check. Resetting search.");
                        _currentIndex = -1; // Reset index and retry on next call maybe
                        break; // Exit current search attempt
                    }


                    TrafficUrlItem currentItem = _dataSource.Items[checkIndex];

                    // --- Filter Criteria: Item needs processing if Current < Required ---
                    bool needsProcessing = currentItem.CurrentQuantity < currentItem.RequireQuantity;

                    if (needsProcessing && !_checkedOutIds.Contains(currentItem.Id))
                    {
                        // Found an available item that needs processing
                        _checkedOutIds.Add(currentItem.Id); // Mark as checked out
                        _currentIndex = checkIndex;        // Update the last index checked
                        System.Diagnostics.Debug.WriteLine($"ClientController: Checking out item ID {currentItem.Id} (Current: {currentItem.CurrentQuantity}, Required: {currentItem.RequireQuantity})");
                        return currentItem; // Return the item
                    }
                    // Else: Either doesn't need processing OR is already checked out, continue loop
                }

                // Looped through the entire list, nothing available found
                System.Diagnostics.Debug.WriteLine($"ClientController: No available items found to process in current loop.");
                return null;
            }
        }

        /// <summary>
        /// Increments the CurrentQuantity of the processed item by 1,
        /// updates it in the DataSource's in-memory collection,
        /// and releases the item (removes it from the checked-out list).
        /// NOTE: This updates the IN-MEMORY data only. Call DataSource.SaveChangesAsync separately.
        /// </summary>
        /// <param name="processedItem">The item that has finished processing (must have the correct ID).</param>
        /// <returns>True if the item was found and updated in the DataSource, False otherwise.</returns>
        public bool UpdateAndReleaseItem(TrafficUrlItem processedItem)
        {
            // Validate the input item (basic check)
            if (processedItem == null || processedItem.Id <= 0) // Use appropriate ID validation (e.g., assuming ID > 0)
            {
                System.Diagnostics.Debug.WriteLine($"ClientController: UpdateAndReleaseItem called with invalid item or ID.");
                return false;
            }

            // --- 1. Increment the Current Quantity internally ---
            // It's crucial that this modification happens on the object reference
            // that is also present in the DataSource's ObservableCollection.
            processedItem.CurrentQuantity += 1;
            System.Diagnostics.Debug.WriteLine($"ClientController: Incremented CurrentQuantity for item ID {processedItem.Id} to {processedItem.CurrentQuantity}.");

            // --- 2. Update the item in the main DataSource ---
            // This call ensures that if the DataSource internally finds the item by ID
            // and updates its properties, the incremented value is used.
            // It also triggers INotifyPropertyChanged if Update calls SetProperty.
            bool updateSuccess = _dataSource.Update(processedItem);

            if (!updateSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"ClientController: DataSource failed to update item ID {processedItem.Id}. Item might have been removed or ID mismatch.");
                // Attempt to release ID anyway, as the client thinks it's done.
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ClientController: Updated item ID {processedItem.Id} in DataSource.");
            }

            // --- 3. Release the item from being checked out ---
            lock (_lock)
            {
                bool removed = _checkedOutIds.Remove(processedItem.Id);
                if (!removed)
                {
                    System.Diagnostics.Debug.WriteLine($"ClientController: Warning - Attempted to release item ID {processedItem.Id}, but it wasn't found in the checked-out list.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ClientController: Released item ID {processedItem.Id}.");
                }
            }

            // Return the status of the underlying DataSource update operation
            return updateSuccess;
        }

        /// <summary>
        /// Resets the controller's state, clearing the list of checked-out items
        /// and resetting the starting index for GetNextItemToProcess.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _checkedOutIds.Clear();
                _currentIndex = -1;
                System.Diagnostics.Debug.WriteLine("ClientController: State reset.");
            }
        }

        /// <summary>
        /// Gets the number of items currently marked as checked out.
        /// </summary>
        public int CheckedOutCount
        {
            get
            {
                lock (_lock)
                {
                    return _checkedOutIds.Count;
                }
            }
        }
    }
}