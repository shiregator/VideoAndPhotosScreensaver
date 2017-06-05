using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SettingsViewModel;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace VideoScreensaver
{
    class SettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { }; 

        private void OnPropertyChanged(String propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public SettingsViewModel()
        {
            _addCommand = new CommandHandler(
                o =>
                {
                    var dial = new CommonOpenFileDialog();
                    dial.Multiselect = false;
                    dial.IsFolderPicker = true;
                    if (
                        dial.ShowDialog(Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive)) ==
                        CommonFileDialogResult.Ok)
                    {
                        _mediaPaths.Add(dial.FileName);
                        PreferenceManager.WriteVideoSettings(_mediaPaths.ToList());
                    }}, 
                o => true);
            _delCommand = new CommandHandler(o =>
            {
                if (!String.IsNullOrWhiteSpace(_selectedRow) && _mediaPaths.Contains(_selectedRow))
                {
                    _mediaPaths.Remove(_selectedRow);
                    PreferenceManager.WriteVideoSettings(_mediaPaths.ToList());
                }
            }, o => !String.IsNullOrWhiteSpace(_selectedRow));

            var list = PreferenceManager.ReadVideoSettings();
            foreach (var item in list)
            {
                _mediaPaths.Add(item);
            }

            Volume = (int)(PreferenceManager.ReadVolumeSetting() * 100);
            NextMediaAlgorithm = PreferenceManager.ReadAlgorithmSetting();
        }

        public int Interval
        {
            get { return PreferenceManager.ReadIntervalSetting(); }
            set { PreferenceManager.WriteIntervalSetting(value); OnPropertyChanged("Interval");}
        }

        private String _selectedRow;

        public String SelectedRow
        {
            get
            {
                return _selectedRow;
            }
            set
            {
                _selectedRow = value; OnPropertyChanged("SelectedRow"); _delCommand.UpdateCanExecute();
            }
        }

        private ObservableCollection<String> _mediaPaths = new ObservableCollection<string>();

        public ObservableCollection<String> MediaPaths
        {
            get { return _mediaPaths; }
        }

        private CommandHandler _addCommand;
        public CommandHandler AddFolderCommand { get { return _addCommand;} }

        private CommandHandler _delCommand;
        public CommandHandler RemoveFromListCommand { get { return _delCommand; } }

        private double _volume;
        public int Volume {
            get
            {
                return (int) (_volume * 100);
            }
            set
            {
                _volume = value / 100F;
                OnPropertyChanged("Volume");
                PreferenceManager.WriteVolumeSetting(_volume);
            }
        }

        private int _alghoritm;
        public int NextMediaAlgorithm
        {
            get
            {
                return _alghoritm;
            }
            set
            {
                _alghoritm = value;
                OnPropertyChanged("NextMediaAlgorithm");
                PreferenceManager.WriteAlgorithmSetting(_alghoritm);
            }
        }
    }
}
