using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualAssetGenerator.Model
{
    class SizeConstraintData : INotifyPropertyChanged
    {
        private string _imageType;
        private int _contentFraction;
        private string _qualifierName;
        private string _title;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string ImageType
        {
            get => _imageType;
            set
            {
                _imageType = value;
                OnPropertyChanged();
            }
        }

        public int ContentFraction
        {
            get => _contentFraction;
            set
            {
                _contentFraction = value;
                OnPropertyChanged();
            }
        }

        public string QualifierName
        {
            get => _qualifierName;
            set
            {
                _qualifierName = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SizeConstraintData Clone()
        {
            return new SizeConstraintData
            {
                ContentFraction = ContentFraction,
                ImageType = ImageType,
                QualifierName = QualifierName,
                Title = Title
            };
        }
    }
}
