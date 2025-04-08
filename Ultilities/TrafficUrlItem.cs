using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic; // Required for EqualityComparer

namespace Terus_Traffic.Ultilities // Change to your actual namespace
{
    public class TrafficUrlItem : INotifyPropertyChanged
    {
        // --- Private Fields ---
        // Match these types to your expected Excel data
        private int _id; // Assuming ID is numeric
        private string _keyword;
        private string _url;
        private int? _rank; // Nullable if Rank can be empty
        private int _requireQuantity;
        private int _currentQuantity;
        private string _type;
        private bool _mobile; // Assuming True/False or 1/0 in Excel

        // --- Public Properties (Match Excel Column Names conceptually) ---
        // Property names follow C# conventions (PascalCase)
        // The DataSource will map these to Excel columns (case might matter in mapping)

        public int Id // Corresponds to "ID" column in Excel
        {
            get => _id;
            // Usually, ID is not settable after creation, but depends on your logic
            set => SetProperty(ref _id, value);
        }

        public string Keyword // Corresponds to "Keyword" column
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public string Url // Corresponds to "URL" column
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public int? Rank // Corresponds to "Rank" column
        {
            get => _rank;
            set => SetProperty(ref _rank, value);
        }

        public int RequireQuantity // Corresponds to "Require Quantity" column
        {
            get => _requireQuantity;
            set => SetProperty(ref _requireQuantity, value);
        }

        public int CurrentQuantity // Corresponds to "Current Quantity" column
        {
            get => _currentQuantity;
            set => SetProperty(ref _currentQuantity, value);
        }

        public string Type // Corresponds to "Type" column
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool Mobile // Corresponds to "Mobile" column
        {
            get => _mobile;
            set => SetProperty(ref _mobile, value);
        }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // --- Optional: Override Equals/GetHashCode based on ID ---
        public override bool Equals(object obj)
        {
            return obj is TrafficUrlItem item && Id == item.Id;
        }

        public override int GetHashCode()
        {
            // Use ID's hash code
            return Id.GetHashCode();
        }
    }
}