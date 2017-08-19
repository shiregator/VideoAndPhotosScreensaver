using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private int currentLastMediaItem = -1;
        private bool isLoadingFiles = false;
        private bool exitEnabled = true; // disable exit when ShoInFolder function called
        private CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private List<String> mediaPaths;
        private List<String> mediaFiles;
        private DispatcherTimer imageTimer;
        private DispatcherTimer timeoutTimer;
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
                timeoutTimer?.Start();
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

            var timeout = PreferenceManager.ReadVolumeTimeoutSetting();
            if (timeout > 0)
            {
                timeoutTimer = new DispatcherTimer();
                timeoutTimer.Interval = TimeSpan.FromMinutes(timeout);
                timeoutTimer.Tick += (obj, e) =>
                {
                    FullScreenMedia.Volume = 0;
                    ShowError("Video volume is muted");
                    infoShowingTimer.Start();
                };
                timeoutTimer.Start();                
            }
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
                    FileInfo fi = new FileInfo(mediaFiles[currentItem]);
                    if (acceptedExtensionsImages.Contains(fi.Extension.ToLower())) // Only rotate images
                        RotateImage();
                    break;
                case Key.S:
                    ShowInFolder();
                    break;
                default:
                    EndFullScreensaver();
                    break;
            }
            e.Handled = true;
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
                      "S - Show/Open file");
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
            Process.Start(mediaFiles[currentItem]);
            EndFullScreensaver();
        }

        private void Pause(bool forcePause = false)
        {
            if (FullScreenImage.Visibility == Visibility.Visible)
            {
                if (imageTimer.IsEnabled || forcePause)
                {
                    imageTimer.Stop();
                } else {
                    HideError();
                    imageTimer.Start();
                }
            } else
            {
                if (GetMediaState(FullScreenMedia) == MediaState.Play || forcePause)
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
            var dial = new PromptDialog("Delete file?", " Type yes or ok if you want to delete " + mediaFiles[currentItem] + " file", "yes,ok");            
            
            if (dial.ShowDialog() == true)
            {
                String fileToDelete = mediaFiles[currentItem];
                // remove filename from list so we don`t use it again
                if (algorithm == PreferenceManager.ALGORITHM_RANDOM)
                {
                    if (lastMedia.IndexOf(fileToDelete) >= currentLastMediaItem)
                        currentLastMediaItem--;
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

                File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PhotoVideoScreensaver_deletedFiles.log"), DateTime.Now + ": " + fileToDelete + Environment.NewLine); //you can add here anything you want. 
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
            if (!preview && exitEnabled) {
                cancellationSource.Cancel();
                Application.Current?.Shutdown();                
                //Close();
            }
        }

        private bool IsMedia(String fileName)
        {
            // Ignore these files
            if (fileName.Contains("$RECYCLE.BIN"))
                return false;

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

        private async Task AddMediaFilesFromDirRecursive(String path, CancellationToken token)
        {
			var files = Directory.GetFiles(path);
            // get all media files using linq
            var media = from String f in files
                        where IsMedia(f)
                        select f;
            // add all files to media list
            foreach (string s in media)
            {
                if (token.IsCancellationRequested) return;
                mediaFiles.Add(Path.Combine(path, s));
            }
            // go through all subfolders
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                await AddMediaFilesFromDirRecursive(dir, token);
            }
		}

        private async Task LoadFiles()
        {
            var tempAlgorithm = algorithm;
			if(algorithm == PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT) algorithm = PreferenceManager.ALGORITHM_RANDOM; //set ALGORITHM_RANDOM until we load all files
			foreach (string videoPath in mediaPaths)
            {
                // Verify path is reachable
                if (!Directory.Exists(videoPath))
                    break;
                await AddMediaFilesFromDirRecursive(videoPath, cancellationSource.Token);
            }
            algorithm = tempAlgorithm;
            if (algorithm == PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT)
            {
                // shuffle list
                mediaFiles = mediaFiles.OrderBy(i => Guid.NewGuid()).ToList();

				// Prepend any stored previous files from loading
				mediaFiles.InsertRange(0, lastMedia);

				// Set index of combined list at last media item
				currentItem = lastMedia.Count - 1;
				Console.WriteLine("LoadFiles(): b lastMedia.Count = " + lastMedia.Count + ", currentItem = " + currentItem + ", mediaFiles.Count = " + mediaFiles.Count);
			}
            if (algorithm == PreferenceManager.ALGORITHM_RANDOM)
            {                
                currentItem = 0; //clear current item to start over
                currentLastMediaItem = 0;
            }
            isLoadingFiles = false;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) {
            mediaPaths = PreferenceManager.ReadVideoSettings();
            mediaFiles = new List<string>();
            algorithm = PreferenceManager.ReadAlgorithmSetting();
            if (algorithm == PreferenceManager.ALGORITHM_RANDOM || algorithm == PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT) // we need to create it before we start showing pictures. before it was after full load
            {
                lastMedia = new List<String>();
            }
            isLoadingFiles = true;
            Task.Factory.StartNew(() => LoadFiles()); // load files in another thread
            if ((mediaPaths.Count == 0 || mediaFiles.Count == 0) && !isLoadingFiles) {
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
                    {
                        if (isLoadingFiles)
                            currentItem = 0;
                        else
                            currentItem = mediaFiles.Count - 1;
                    }
                    break;
                case PreferenceManager.ALGORITHM_RANDOM:
                    if (lastMedia.Count >= 2)
                    {
                        if (currentLastMediaItem > 0) currentLastMediaItem--;
                        currentItem = mediaFiles.IndexOf(lastMedia[currentLastMediaItem]);
                        //lastMedia.RemoveAt(lastMedia.Count - 1);
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
            if (isLoadingFiles)
            {
                if ((currentItem + 1) >= mediaFiles.Count || mediaFiles.Count == 0) // first condition is for ALGORITHM_SEQUENTIAL and ALGORITHM_RANDOM_NO_REPEAT , second is for ALGORITHM_RANDOM
                {
                    ShowError("Wait untill more files loaded");
                    infoShowingTimer.Start();
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
                    do
                    {
                        Thread.Sleep(100);
                    } while (isLoadingFiles && currentItem >= mediaFiles.Count);
                }
            }
            switch (algorithm)
            {
                case PreferenceManager.ALGORITHM_SEQUENTIAL:
                case PreferenceManager.ALGORITHM_RANDOM_NO_REPEAT:
					sleepAtStart();
					currentItem++;                    
                    if (currentItem >= mediaFiles.Count)
                    {
                        currentItem = 0;
                    }
                    break;
                case PreferenceManager.ALGORITHM_RANDOM:
					sleepAtStart();
					if (currentLastMediaItem < (lastMedia.Count - 1))
                    {
                        currentLastMediaItem++;
                        currentItem = mediaFiles.IndexOf(lastMedia[currentLastMediaItem]);
                    }else
                    {
                        currentItem = new Random().Next(mediaFiles.Count - 1);                        
                        lastMedia.Add(mediaFiles[currentItem]);
                        if (lastMedia.Count > 100)
                            lastMedia.RemoveAt(0);
                        currentLastMediaItem = lastMedia.Count - 1;
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


        private void LoadImage(string filename)
        {
            FullScreenImage.RenderTransform = null;
            FullScreenImage.Visibility = Visibility.Visible;
            FullScreenMedia.Visibility = Visibility.Collapsed;

            Overlay.Text = "";

            if (Path.GetExtension(filename).ToLower() == ".jpg") // load exif only for jpg
            {
                UInt16 orient = 1;
                try
                {
                    var exif = new ExifUtils();
                    if (exif.ReadExifFromFile(filename))
                    {
                        orient = exif.GetOrientation();
                        Overlay.Text = filename + Environment.NewLine + exif.GetInfoString();
                    }
                    else
                    {
                        Overlay.Text = "";
                    }
                }
                catch
                {
                    ShowError("Can not load exif data");
                    infoShowingTimer.Start();
                }

                // Rotate image per user request (R key)
                if (imageRotationAngle == 90)
                {
                    try
                    {
                        orient = ExifUtils.RotateImageViaInPlaceBitmapMetadataWriter(filename, orient);
                    }
                    catch //InPlaceBitmapMetadataWriter can only work when there is already orientation exif. if image doesn`t have it we will use transcoding
                    {
                        orient = ExifUtils.RotateImageViaTranscoding(filename, orient);
                    }
                }

                // Get rotation angle per EXIF orientation
                var fType = ExifUtils.GetRotateFlipTypeByExifOrientationData(orient);
                imageRotationAngle = ExifUtils.GetBitmapRotationAngleByRotationFlipType(fType);
            } else //if (Path.GetExtension(filename).ToLower() == ".jpg")
            {
                if (imageRotationAngle == 90)
                {
                    try
                    {
                        //rotate other types of image using Image class
                        using (FileStream imgStream = File.Open(filename, FileMode.Open, FileAccess.ReadWrite))
                        using (Image imgForRotation = Image.FromStream(imgStream, false, false))
                        {
                            imgForRotation.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
                            imgStream.Seek(0, SeekOrigin.Begin);
                            switch (Path.GetExtension(filename).ToLower())
                            {
                                case ".png":
                                    imgForRotation.Save(imgStream, ImageFormat.Png);
                                    break;
                                case ".bmp":
                                    imgForRotation.Save(imgStream, ImageFormat.Bmp);
                                    break;
                                case ".gif":
                                    imgForRotation.Save(imgStream, ImageFormat.Gif);
                                    break;
                            }
                            imgStream.Flush();
                            imgStream.Close();
                        }
                        imageRotationAngle = 0; // because we already have rotated image
                    }
                    catch (Exception e)
                    {
                        ShowError(e.Message);
                        infoShowingTimer.Start();
                    }
                }
            }            

            try
            {
                using (
                    var imgStream = File.Open(filename, FileMode.Open, FileAccess.Read,
                        FileShare.Delete | FileShare.Read))
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;

                    img.StreamSource = imgStream; // load image from stream instead of file
                    img.EndInit();

                    // Rotate Image if necessary
                    TransformedBitmap transformBmp = new TransformedBitmap();
                    transformBmp.BeginInit();
                    transformBmp.Source = img;
                    RotateTransform transform = new RotateTransform(imageRotationAngle);
                    transformBmp.Transform = transform;
                    transformBmp.EndInit();
                    FullScreenImage.Source = transformBmp;
                    // Initialize rotation variable for next image
                    imageRotationAngle = 0;

                    imageTimer.Start();
                    
                    //if we failed to get exif data set some basic info
                    if (String.IsNullOrWhiteSpace(Overlay.Text))
                    {
                        Overlay.Text = filename + "\n" + (int)img.Width + "x" + (int)img.Height;
                    }
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

        private void sleepAtStart()
        {
            if (isLoadingFiles && currentItem <= 0)
            {
                // Sleep to buffer some images to start
                Console.WriteLine("NextMediaItem(): isLoadingFiles = " + isLoadingFiles + ", currentItem = " + currentItem + ", mediaFiles.Count = " + mediaFiles.Count + ", algorithm = " + algorithm);
                Thread.Sleep(1000);
            }
        }
    }
}
