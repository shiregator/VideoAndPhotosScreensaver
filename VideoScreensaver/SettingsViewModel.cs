using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using SettingsViewModel;
using Application = System.Windows.Application;

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
            // command that show folder selection dialog and add selected folder to list
            _addCommand = new CommandHandler(
                o =>
                {
                    var dial = new FolderBrowserDialog();
                    if (
                        dial.ShowDialog(null) == DialogResult.OK)
                    {
                        _mediaPaths.Add(dial.SelectedPath);
                        PreferenceManager.WriteVideoSettings(_mediaPaths.ToList());
                    }}, 
                o => true);
            // command that delete selected folder from list
            _delCommand = new CommandHandler(o =>
            {
                if (!String.IsNullOrWhiteSpace(_selectedRow) && _mediaPaths.Contains(_selectedRow))
                {
                    _mediaPaths.Remove(_selectedRow);
                    PreferenceManager.WriteVideoSettings(_mediaPaths.ToList());
                }
            }, o => !String.IsNullOrWhiteSpace(_selectedRow));

            // command that will remove all registry keys
            _removeSettingsCommand = new CommandHandler(o =>
            {
                if (System.Windows.MessageBox.Show("Are you sure you want to remove all settings from registry?", "Remove all settings", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    PreferenceManager.RemoveRegistryKeys();
            }, o => true);

            // list of folders
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

        public int VolumeTimeout
        {
            get { return PreferenceManager.ReadVolumeTimeoutSetting(); }
            set { PreferenceManager.WriteVolumeTimeoutSetting(value); OnPropertyChanged("VolumeTimeout"); }
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

        private CommandHandler _removeSettingsCommand;
        public CommandHandler RemoveSettingsCommand { get { return _removeSettingsCommand; } }

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
