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
                using (FileStream imgStream = File.Open(filename, FileMode.Open, FileAccess.ReadWrite))
                {
                    if (Path.GetExtension(filename).ToLower() == ".jpg") // load exif only for jpg
                    {
                        UInt16 orient = 1;
                        System.Drawing.RotateFlipType fType = System.Drawing.RotateFlipType.RotateNoneFlipNone;
                        StringBuilder info = new StringBuilder();
                        var decoder = new JpegBitmapDecoder(imgStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var bitmapFrame = decoder.Frames[0];

                        if (bitmapFrame != null)
                        {
                            BitmapMetadata metaData = (BitmapMetadata)bitmapFrame.Metadata.Clone();

                            info.AppendLine(filename + "\n" + (int)bitmapFrame.Width + "x" + (int)bitmapFrame.Height);
                            if (metaData != null)
                            {
                                if (!String.IsNullOrWhiteSpace(metaData.DateTaken))
                                {
                                    info.AppendLine("Date taken: " + metaData.DateTaken);
                                }
                                if (!String.IsNullOrWhiteSpace(metaData.Title))
                                {
                                    info.AppendLine("Title: " + metaData.Title);
                                }
                                if (!String.IsNullOrWhiteSpace(metaData.Subject))
                                {
                                    info.AppendLine("Subject: " + metaData.Subject);
                                }
                                if (!String.IsNullOrWhiteSpace(metaData.Comment))
                                {
                                    info.AppendLine("User comment: " + metaData.Comment);
                                }

                                //PrintMetadata(decoder.Frames[0].Metadata, string.Empty);
                                String xmpSubject = (String)metaData.GetQuery("/xmp/dc:subject/{ulong=0}");
                                if (!String.IsNullOrWhiteSpace(xmpSubject))
                                {
                                    info.Append("Subject: " + xmpSubject);
                                    int i = 1;
                                    while (!String.IsNullOrWhiteSpace(xmpSubject))
                                    {
                                        xmpSubject = (String)metaData.GetQuery("/xmp/dc:subject/{ulong=" + i + "}");
                                        if (!String.IsNullOrWhiteSpace(xmpSubject))
                                            info.Append(", " + xmpSubject);
                                        i++;
                                    }
                                    info.AppendLine();
                                }

                                object keywords = metaData.GetQuery("/app13/{ushort=0}/{ulonglong=61857348781060}/iptc/{str=Keywords}");
                                if (keywords.GetType() == typeof(string))
                                    info.AppendLine("Keywords: " + keywords);
                                else if ((keywords.GetType() == typeof(string[])))
                                {
                                    if ( ((string[])keywords).Length > 0 )
                                    {
                                        info.Append("Keywords: " + ((string[])keywords)[0]);
                                        for (int i = 1; i < ((string[])keywords).Length; i++)
                                        {
                                            info.Append(", " + ((string[])keywords)[i]);
                                        }
                                        info.AppendLine();
                                    }
                                }

                                // Get rotation orientation
                                if (metaData.ContainsQuery(@"/app1/{ushort=0}/{ushort=274}"))
                                {
                                    orient = (UInt16)metaData.GetQuery(@"/app1/{ushort=0}/{ushort=274}"); //get rotation
                                }
                                                                                                
                            }
                        }
                        Overlay.Text = info.ToString();
                        imgStream.Flush();
                        imgStream.Close();

                        // Rotate image per user request (R key)
                        if (imageRotationAngle == 90)
                        {
                            orient = RotateImageViaInPlaceBitmapMetadataWriter(filename, orient);
                        }

                        // Get rotation angle per EXIF orientation
                        fType = GetRotateFlipTypeByExifOrientationData(orient);
                        imageRotationAngle = GetBitmapRotationAngleByRotationFlipType(fType);

                    }
                    else //if (Path.GetExtension(filename).ToLower() == ".jpg")
                    {
                        if (imageRotationAngle == 90)
                        {
                            //rotate other types of image using Image class
                            using (Image imgForRotation = Image.FromStream(imgStream, false, false))
                            {
                                Overlay.Text = filename + "\n" + imgForRotation.Width + "x" + imgForRotation.Height;
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

                            }
                            imgStream.Flush();
                            imgStream.Close();
                            imageRotationAngle = 0; // because we already have rotated image
                        } else
                        {
                            Overlay.Text = ""; // we will set it bellow
                        }
                    }
                }
            }
            catch
            {
                Overlay.Text = "";
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
                        Overlay.Text = filename + "\n" + img.Width + "x" + img.Height;
                    }
                }
            }
            catch
            {
                FullScreenImage.Source = null;
                ShowError("Can not load " + filename + " ! Screensaver paused, press P to unpause.");
            }
        }

        private UInt16 RotateImageViaTranscoding(string filename, UInt16 prevOrient)
        {
            UInt16 orient = 1;
            orient = (UInt16)GetNextRotationOrientation(prevOrient); // get new rotation

            // This only works for jpg photos
            if (!filename.EndsWith("jpg"))
            {
                Console.WriteLine("The file you passed in is not a JPEG.");
                ShowError("The file you passed in is not a JPEG.");
                return 0;
            }

            // This code is based on http://blogs.msdn.com/b/rwlodarc/archive/2007/07/18/using-wpf-s-inplacebitmapmetadatawriter.aspx
            BitmapCreateOptions createOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
            string outputTempFile = filename + "_out.jpg";
            using (Stream originalFile = File.Open(filename, FileMode.Open, FileAccess.ReadWrite))
            {
                // This method uses a lossless transcode operation and stores metadata changes in a temp file before copying them
                // back to the original file
                BitmapDecoder original = BitmapDecoder.Create(originalFile, createOptions, BitmapCacheOption.None);

                JpegBitmapEncoder output = new JpegBitmapEncoder();

                // If you're just interested in doing a lossless transcode without adding metadata, just do this:
                //output.Frames = original.Frames;

                // If you want to add metadata to the image (or could use the InPlaceBitmapMetadataWriter with added padding)
                if (original.Frames[0] != null && original.Frames[0].Metadata != null)
                {
                    // The BitmapMetadata object is frozen. So, you need to clone the BitmapMetadata and then
                    // set the padding on it. Lastly, you need to create a "new" frame with the updated metadata.
                    BitmapMetadata metadata = original.Frames[0].Metadata.Clone() as BitmapMetadata;

                    // Of the metadata handlers that we ship in WIC, padding can only exist in IFD, EXIF, and XMP.
                    // Third parties implementing their own metadata handler may wish to support IWICFastMetadataEncoder
                    // and hence support padding as well.
                    /*
                    metadata.SetQuery("/app1/ifd/PaddingSchema:Padding", paddingAmount);
                    metadata.SetQuery("/app1/ifd/exif/PaddingSchema:Padding", paddingAmount);
                    metadata.SetQuery("/xmp/PaddingSchema:Padding", paddingAmount);

                    // Since you're already adding metadata now, you can go ahead and add metadata up front.
                    metadata.SetQuery("/app1/ifd/{uint=897}", "hello there");
                    metadata.SetQuery("/app1/ifd/{uint=898}", "this is a test");
                    metadata.Title = "This is a title";
                    */

                    metadata.SetQuery("/app1/{ushort=0}/{ushort=274}", orient); //set next rotation  

                    // Create a new frame identical to the one from the original image, except the metadata changes.
                    // Essentially we want to keep this as close as possible to:
                    //     output.Frames = original.Frames;
                    output.Frames.Add(BitmapFrame.Create(original.Frames[0], original.Frames[0].Thumbnail, metadata, original.Frames[0].ColorContexts));
                }

                using (Stream outputFile = File.Open(outputTempFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    output.Save(outputFile);
                }
            }

            // Delete the original and replace it with the temp output file
            File.Delete(filename);
            File.Move(outputTempFile, filename);

            return orient;
        }

        private UInt16 RotateImageViaInPlaceBitmapMetadataWriter(string filename, UInt16 prevOrient)
        {
            // InPlaceBitmapMetadataWriter allows us to modify the metadata (exif) directly to the original file (without a temp file)
            // assuming there is enough space, otherwise padding must be added.  For orientation the space is constant, so it should
            // always work if the orientation field already exists
            UInt16 orient = 1;
            orient = (UInt16)GetNextRotationOrientation(prevOrient); // get new rotation

            // This only works for jpg photos
            if (!filename.EndsWith("jpg"))
            {
                Console.WriteLine("The file you passed in is not a JPEG.");
                ShowError("The file you passed in is not a JPEG.");
                return 0;
            }

            // This code is based on http://blogs.msdn.com/b/rwlodarc/archive/2007/07/18/using-wpf-s-inplacebitmapmetadatawriter.aspx
            using (Stream originalFile = File.Open(filename, FileMode.Open, FileAccess.ReadWrite))
            {
                ConsoleColor originalColor = Console.ForegroundColor;

                BitmapDecoder output = BitmapDecoder.Create(originalFile, BitmapCreateOptions.None, BitmapCacheOption.Default);

                InPlaceBitmapMetadataWriter metadata = output.Frames[0].CreateInPlaceBitmapMetadataWriter();

                // Within the InPlaceBitmapMetadataWriter, you can add, update, or remove metadata.
                metadata.SetQuery("/app1/{ushort=0}/{ushort=274}", orient); //set next rotation  

                if (metadata.TrySave())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("InPlaceMetadataWriter succeeded!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("InPlaceMetadataWriter failed!");
                    ShowError("Rotate Image failed. Try to 'S' (Show in Folder) and manually rotate it.");
                }

                Console.ForegroundColor = originalColor;

                return orient;
            }

        }

        private void PrintMetadata(System.Windows.Media.ImageMetadata metadata, string fullQuery)
        {
            BitmapMetadata theMetadata = metadata as BitmapMetadata;
            if (theMetadata != null)
            {
                foreach (string query in theMetadata)
                {
                    string tempQuery = fullQuery + query;
                    // query string here is relative to the previous metadata reader.
                    object o = theMetadata.GetQuery(query);
                    //richTextBox1.Text += "\n" + tempQuery + ", " + query + ", " + o;
                    Console.WriteLine(tempQuery + ", " + query + ", " + o);
                    BitmapMetadata moreMetadata = o as BitmapMetadata;
                    if (moreMetadata != null)
                    {
                        PrintMetadata(moreMetadata, tempQuery);
                    }
                }
            }
        }

        /// <summary>
        /// Return the proper System.Drawing.RotateFlipType according to given orientation EXIF metadata
        /// </summary>
        /// <param name="orientation">Exif "Orientation"</param>
        /// <returns>the corresponding System.Drawing.RotateFlipType enum value</returns>
        private static System.Drawing.RotateFlipType GetRotateFlipTypeByExifOrientationData(int orientation)
        {
            switch (orientation)
            {
                case 1:
                default:
                    return System.Drawing.RotateFlipType.RotateNoneFlipNone;
                case 2:
                    return System.Drawing.RotateFlipType.RotateNoneFlipX;
                case 3:
                    return System.Drawing.RotateFlipType.Rotate180FlipNone;
                case 4:
                    return System.Drawing.RotateFlipType.Rotate180FlipX;
                case 5:
                    return System.Drawing.RotateFlipType.Rotate90FlipX;
                case 6:
                    return System.Drawing.RotateFlipType.Rotate90FlipNone;
                case 7:
                    return System.Drawing.RotateFlipType.Rotate270FlipX;
                case 8:
                    return System.Drawing.RotateFlipType.Rotate270FlipNone;
            }
        }

        private int GetBitmapRotationAngleByRotationFlipType(System.Drawing.RotateFlipType rotationFlipType)
        {
            switch (rotationFlipType)
            {
                case System.Drawing.RotateFlipType.RotateNoneFlipNone:
                default:
                    return 0;
                case System.Drawing.RotateFlipType.Rotate90FlipNone:
                    return 90;
                case System.Drawing.RotateFlipType.Rotate180FlipNone:
                    return 180;
                case System.Drawing.RotateFlipType.Rotate270FlipNone:
                    return 270;
            }
        }


        private int GetNextRotationOrientation(int currentOrientation)
        {
            switch (currentOrientation)
            {
                case 1:       // System.Drawing.RotateFlipType.RotateNoneFlipNone
                    return 6; // System.Drawing.RotateFlipType.Rotate90FlipNone

                case 3:       // System.Drawing.RotateFlipType.Rotate180FlipNone
                    return 8; // System.Drawing.RotateFlipType.Rotate270FlipNone

                case 6:       // System.Drawing.RotateFlipType.Rotate90FlipNone
                    return 3; // System.Drawing.RotateFlipType.Rotate180FlipNone

                case 8:       // System.Drawing.RotateFlipType.Rotate270FlipNone
                    return 1; // System.Drawing.RotateFlipType.RotateNoneFlipNone

                default:
                    ShowError("Could not determine next rotation orientation.");
                    return currentOrientation;
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
