using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Image = System.Drawing.Image;

namespace VideoScreensaver {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private bool preview;
        private Point? lastMousePosition = null;  // Workaround for "MouseMove always fires when maximized" bug.
        private int currentItem = -1;
        private List<String> mediaPaths;
        private List<String> mediaFiles;
        private DispatcherTimer imageTimer;
        private DispatcherTimer infoShowingTimer;
        private List<String> acceptedExtensionsImages = new List<string>() {".jpg", ".png", ".bmp", ".gif"};
        private List<String> acceptedExtensionsVideos = new List<string>() { ".avi", ".wmv", ".mpg", ".mpeg", ".mkv", ".mp4" };
        private List<String> lastMedia; // store last 100 of random files
        private int algorithm;
        private int imageRotationAngle;
        private double volume {
            get { return FullScreenMedia.Volume; }
            set {
                FullScreenMedia.Volume = Math.Max(Math.Min(value, 1), 0);
                PreferenceManager.WriteVolumeSetting(FullScreenMedia.Volume);
            }
        }

        public MainWindow(bool preview) {
            InitializeComponent();
            this.preview = preview;
            FullScreenMedia.Volume = PreferenceManager.ReadVolumeSetting();
            imageTimer = new DispatcherTimer();
            imageTimer.Tick += ImageTimerEnded;
            imageTimer.Interval = TimeSpan.FromMilliseconds(PreferenceManager.ReadIntervalSetting());
            infoShowingTimer = new DispatcherTimer();
            infoShowingTimer.Tick += (sender, args) => HideError();
            infoShowingTimer.Interval = TimeSpan.FromSeconds(5);
            if (preview) {
                ShowError("When fullscreen, control volume with up/down arrows or mouse wheel.");
            }
            // setting overlay text when media is opened. if you will try to set it in LoadMedia you will get nothing because media is not loaded yet
            FullScreenMedia.MediaOpened += (sender, args) =>
            {
                if (FullScreenMedia.Source != null)
                Overlay.Text = FullScreenMedia.Source.AbsolutePath + "\n" +
                               FullScreenMedia.NaturalVideoWidth + "x" + FullScreenMedia.NaturalVideoHeight + "\n" +
                               (FullScreenMedia.NaturalDuration.HasTimeSpan
                                   ? FullScreenMedia.NaturalDuration.TimeSpan.ToString()
                                   : "");
            };
        }

        //dirty trick to check if mediaelement is playing or paused
        private MediaState GetMediaState(MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            MediaState state = (MediaState)stateField.GetValue(helperObject);
            return state;
        }

        private void ScrKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Up:
                case Key.VolumeUp:
                    volume += 0.1;
                    break;
                case Key.Down:
                case Key.VolumeDown:
                    volume -= 0.1;
                    break;
                case Key.VolumeMute:
                case Key.D0:
                    volume = 0;
                    break;
				case Key.Right:
					imageTimer.Stop();
					NextMediaItem();
					break;
                case Key.Left:
                    imageTimer.Stop();
                    PrevMediaItem();
                    break;
                case Key.P:
                    Pause();
                    break;
                case Key.Delete:
                    imageTimer.Stop();
                    FullScreenMedia.Pause();
                    PromtDeleteCurrentMedia();
                    break;
                case Key.I:
                    Overlay.Visibility = Overlay.Visibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    break;
                case Key.H:
                case Key.OemQuestion:
                    ShowUsage();
                    break;
                case Key.R:
                    RotateImage();
                    break;
                case Key.S:
                    ShowInFolder();
                    break;
                default:
                    EndFullScreensaver();
                    break;
            }
        }

        private void ShowUsage()
        {
            ShowError("Usage of key shortcuts:\n " +
                      "Up - Volume up\n " +
                      "Down - Volume down\n " +
                      "0 - Mute volume\n " +
                      "Right arrow - next image/video\n " +
                      "Left arrow - previous image/video\n " +
                      "P - Pause/unpause\n " +
                      "Delete - Delete current file \n " +
                      "I - Show info overlay\n " +
                      "H - Show this message\n " +
                      "R - Rotate image\n " +
                      "S - Show file in explorer");
            infoShowingTimer.Start();
        }


        private void RotateImage()
        {
            imageRotationAngle += 90;
            imageTimer.Stop();
            LoadImage(mediaFiles[currentItem]);
        }

        private void ShowInFolder()
        {
            Process.Start("explorer", "/select, \"" + mediaFiles[currentItem] + "\"");
            EndFullScreensaver(); // close screensaver to show opened fodlder
        }

        private void Pause()
        {
            if (FullScreenImage.Visibility == Visibility.Visible)
            {
                if (imageTimer.IsEnabled)
                {
                    imageTimer.Stop();
                } else {
                    HideError();
                    imageTimer.Start();
                }
            } else
            {
                if (GetMediaState(FullScreenMedia) == MediaState.Play)
                {
                    FullScreenMedia.Pause();
                } else {
                    HideError();
                    FullScreenMedia.Play();
                }
            }
        }

        private void PromtDeleteCurrentMedia()
        {
            if (
                MessageBox.Show(this, "You want to delete " + mediaFiles[currentItem] + " file?", "Delete file?",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                String fileToDelete = mediaFiles[currentItem];
                // remove filename from list so we don`t use it again
                if (algorithm == PreferenceManager.ALGORITHM_RANDOM)
                {
                    lastMedia.Remove(fileToDelete);
                }
                mediaFiles.RemoveAt(currentItem);
                

                PrevMediaItem();
                try
                {
                    File.Delete(fileToDelete);
                }
                catch
                {
                    Pause(); //pause screensaver
                    MessageBox.Show(this, "Can not delete " + fileToDelete + " ! Please check it and delete manualy!",
                        "Can not delete file!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Pause(); //unpause
                }
            }
            else
            {
                if (FullScreenImage.Visibility == Visibility.Visible)
                    imageTimer.Start(); // start timer because we stoped it on Delete key press
                if (FullScreenMedia.Visibility == Visibility.Visible)
                    FullScreenMedia.Play(); // start again because we paused it on Delete key press
            }
        }

        private void ScrMouseWheel(object sender, MouseWheelEventArgs e) {
            volume += e.Delta / 1000.0;
        }

        private void ScrMouseMove(object sender, MouseEventArgs e) {
            // Workaround for bug in WPF.
            Point mousePosition = e.GetPosition(this);
            if (lastMousePosition != null && mousePosition != lastMousePosition) {
                EndFullScreensaver();
            }
            lastMousePosition = mousePosition;
        }

        private void ScrMouseDown(object sender, MouseButtonEventArgs e) {
            EndFullScreensaver();
        }
        
        // End the screensaver only if running in full screen. No-op in preview mode.
        private void EndFullScreensaver() {
            if (!preview) {
                Application.Current?.Shutdown();
                //Close();
            }
        }

        private bool IsMedia(String fileName)
        {
            foreach (var acceptedExtension in acceptedExtensionsImages)
            {
                if (fileName.ToLower().EndsWith(acceptedExtension))
                    return true;
            }
            foreach (var acceptedExtension in acceptedExtensionsVideos)
            {
                if (fileName.ToLower().EndsWith(acceptedExtension))
                    return true;
            }
            return false;
        }

        private void AddMediaFilesFromDirRecursive(String path)
        {
            var files = Directory.GetFiles(path);
            // get all media files using linq
            var media = from String f in files
                        where IsMedia(f)
                        select f;
            // add all files to media list
            foreach (string s in media)
            {
                mediaFiles.Add(System.IO.Path.Combine(path, s));
            }
            // go through all subfolders
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                AddMediaFilesFromDirRecursive(dir);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            mediaPaths = PreferenceManager.ReadVideoSettings();
            mediaFiles = new List<string>();
            algorithm = PreferenceManager.ReadAlgorithmSetting();
            foreach (string videoPath in mediaPaths)
            {
                AddMediaFilesFromDirRecursive(videoPath);
            }
            if (algorithm == PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT)
            {
                // shuffle list
                mediaFiles = mediaFiles.OrderBy(i => Guid.NewGuid()).ToList();
            }
            if (algorithm == PreferenceManager.ALGORITHM_RANDOM)
            {
                lastMedia = new List<String>();
            }

            if (mediaPaths.Count == 0 || mediaFiles.Count == 0) {
                ShowError("This screensaver needs to be configured before any video is displayed.");
            } else
            {
                NextMediaItem();
            }
        }

        private void PrevMediaItem()
        {
            FullScreenMedia.Stop();
            FullScreenMedia.Source = null; // FIXED Overlay display info is correct on video until you use forward/back arrow keys to traverse to images.
            imageRotationAngle = 0;
            switch (algorithm)
            {
                case PreferenceManager.ALGORITHM_SEQUENTIAL:
                case PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT:
                    currentItem--;
                    if (currentItem < 0)
                        currentItem = mediaFiles.Count - 1;
                    break;
                case PreferenceManager.ALGORITHM_RANDOM:
                    if (lastMedia.Count >= 2)
                    {
                        currentItem = mediaFiles.IndexOf(lastMedia[lastMedia.Count - 2]);
                        lastMedia.RemoveAt(lastMedia.Count - 1);
                    }
                    else
                    {
                        imageTimer.Start();
                    }
                    break;
            }
            if (mediaFiles.Count == 0)
            {
                ShowError("There are no files to show!");
                FullScreenImage.Source = null;
                FullScreenMedia.Stop();
                FullScreenMedia.Source = null;
                return;
            }

            FileInfo fi = new FileInfo(mediaFiles[currentItem]);
            if (acceptedExtensionsImages.Contains(fi.Extension.ToLower())) // check if it image or video
            {
                LoadImage(fi.FullName);
            }
            else
            {
                LoadMedia(fi.FullName);
            }
        }

        private void NextMediaItem()
        {
            FullScreenMedia.Stop();
            FullScreenMedia.Source = null; // FIXED Overlay display info is correct on video until you use forward/back arrow keys to traverse to images.
            imageRotationAngle = 0;
            switch (algorithm)
            {
                case PreferenceManager.ALGORITHM_SEQUENTIAL:
                case PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT:
                    currentItem++;
                    if (currentItem >= mediaFiles.Count)
                        currentItem = 0;
                    break;
                case PreferenceManager.ALGORITHM_RANDOM:
                    currentItem = new Random().Next(mediaFiles.Count);
                    lastMedia.Add(mediaFiles[currentItem]);
                    if (lastMedia.Count > 100)
                        lastMedia.RemoveAt(0);
                    break;
            }
            if (mediaFiles.Count == 0)
            {
                ShowError("There are no files to show!");
                FullScreenImage.Source = null;
                FullScreenMedia.Stop();
                FullScreenMedia.Source = null;
                return;
            }

            FileInfo fi = new FileInfo(mediaFiles[currentItem]);
            if (acceptedExtensionsImages.Contains(fi.Extension.ToLower())) // check if it image or video
            {
                LoadImage(fi.FullName);
            }
            else
            {
                LoadMedia(fi.FullName);
            }
        }


        private void LoadImage(string filename)
        {
            FullScreenImage.RenderTransform = null;
            FullScreenImage.Visibility = Visibility.Visible;
            FullScreenMedia.Visibility = Visibility.Collapsed;
            try
            {
                using (
                    var imgStream = File.Open(filename, FileMode.Open, FileAccess.Read,
                        FileShare.Delete | FileShare.ReadWrite))
                {
                    using (Image imgForExif = Image.FromStream(imgStream, false, false))
                    {
                        StringBuilder info = new StringBuilder();
                        info.AppendLine(filename + "\n" + imgForExif.Width + "x" + imgForExif.Height);
                        foreach (var propertyItem in imgForExif.PropertyItems)
                        {
                            switch (propertyItem.Id)
                            {
                                case 0x9003: // Date taken
                                    DateTime dt;
                                    if (DateTime.TryParseExact(
                                        Encoding.UTF8.GetString(propertyItem.Value).TrimEnd('\0'), "yyyy:dd:MM HH:mm:ss",
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                        info.AppendLine("Date taken: " + dt);
                                    break;
                                case 0x0320: // Title
                                    info.AppendLine("Title: " +
                                                    Encoding.UTF8.GetString(propertyItem.Value).TrimEnd('\0'));
                                    break;
                                case 0x010E: // Description
                                    var descr = Encoding.UTF8.GetString(propertyItem.Value).TrimEnd('\0');
                                    if (!String.IsNullOrWhiteSpace(descr))
                                        info.AppendLine("Description: " + descr);
                                    break;
									/*
                                case 0x9286: // User comment
                                    if (propertyItem.Value.Length < 8)
                                        break; // check length before we will try to get info
                                    string characterCode = Encoding.ASCII.GetString(propertyItem.Value, 0, 8).TrimEnd('\0'); ;
                                    switch (characterCode)
                                    {
                                        case "UNICODE":
                                            info.AppendLine("User comment: " +
                                                            Encoding.Unicode.GetString(propertyItem.Value, 8,
                                                                propertyItem.Len - 8));
                                            break;
                                        case "ASCII":
                                            info.AppendLine("User comment: " +
                                                            Encoding.ASCII.GetString(propertyItem.Value, 8,
                                                                propertyItem.Len - 8));
                                            break;
                                    }
                                    break;
									*/
                            }
                        }
                        Overlay.Text = info.ToString();
                    }
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    //img.UriSource = new Uri(filename);
                    imgStream.Seek(0, SeekOrigin.Begin); // seek stream to beginning
                    img.StreamSource = imgStream; // load image from stream instead of file
                    img.EndInit();
                    TransformedBitmap transformBmp = new TransformedBitmap();
                    transformBmp.BeginInit();
                    transformBmp.Source = img;
                    RotateTransform transform = new RotateTransform(imageRotationAngle);
                    transformBmp.Transform = transform;
                    transformBmp.EndInit();
                    FullScreenImage.Source = transformBmp;
                    imageTimer.Start();
                }
            }
            catch
            {
                FullScreenImage.Source = null;
                ShowError("Can not load " + filename + " ! Screensaver paused, press P to unpause.");
            }
        }

        private void LoadMedia(string filename)
        {
            FullScreenImage.Visibility = Visibility.Collapsed;
            FullScreenMedia.Visibility = Visibility.Visible;
            FullScreenMedia.Source = new Uri(filename);
            FullScreenMedia.Play();
        }

        private void ShowError(string errorMessage) {
            ErrorText.Text = errorMessage;
            ErrorText.Visibility = System.Windows.Visibility.Visible;
            if (preview) {
                ErrorText.FontSize = 12;
            }
        }

        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void MediaEnded(object sender, RoutedEventArgs e) {
            FullScreenMedia.Position = new TimeSpan(0);
            FullScreenMedia.Stop();
            FullScreenMedia.Source = null;
            NextMediaItem();
        }
        
        private void ImageTimerEnded(object sender, EventArgs e)
        {
            imageTimer.Stop();
            FullScreenImage.Source = null;
            NextMediaItem();
        }
    }
}
