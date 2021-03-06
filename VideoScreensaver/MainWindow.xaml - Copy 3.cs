﻿using System;
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
                using (
                    var imgStream = File.Open(filename, FileMode.Open, FileAccess.Read,
                        FileShare.Delete | FileShare.ReadWrite))
                {
					using (Image imgForExif = Image.FromStream(imgStream, false, false))
					{
						// Check to see if image display needs to be rotated per EXIF Orientation parameter (274) or user R key input
						if (Array.IndexOf(imgForExif.PropertyIdList, 274) > -1)
						{
							PropertyItem orientation = imgForExif.GetPropertyItem(274);
							var fType = GetRotateFlipTypeByExifOrientationData( (int)orientation.Value[0] );

							// Check to see if user requested rotation (R key)
							if (imageRotationAngle == 90)
							{
								orientation.Value = BitConverter.GetBytes((int) GetNextRotationOrientation( (int)orientation.Value[0] ));
								// Set EXIF tag property to new orientation
								imgForExif.SetPropertyItem(orientation);
								// update RotateFlipType accordingly
								fType = GetRotateFlipTypeByExifOrientationData((int)orientation.Value[0]);

								//Rotate90(filename);

								imgForExif.SetPropertyItem(imgForExif.PropertyItems[0]);

								// Save rotation to file                            
								imgForExif.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
								switch(Path.GetExtension(filename).ToLower())
								{
									case ".jpg":
										imgForExif.Save(filename, ImageFormat.Jpeg);
										break;
									case ".png":
										imgForExif.Save(filename, ImageFormat.Png);
										break;
									case ".bmp":
										imgForExif.Save(filename, ImageFormat.Bmp);
										break;
									case ".gif":
										imgForExif.Save(filename, ImageFormat.Gif);
										break;
								}
							}

							/*
							// Rotate display of image accordingly
							fType = GetRotateFlipTypeByExifOrientationData((int)orientation.Value[0]);
							if (fType != System.Drawing.RotateFlipType.RotateNoneFlipNone)
							{
								imgForExif.RotateFlip(fType);
							}
							*/

							// Get rotation angle accordingly
							imageRotationAngle = GetBitmapRotationAngleByRotationFlipType( fType );
						}
                    }

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;

                    //img.UriSource = new Uri(filename);
                    imgStream.Seek(0, SeekOrigin.Begin); // seek stream to beginning
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

					//FullScreenImage.Source = img;
                    imageTimer.Start();

                    //********* NEW EXIF CODE **************
                    imgStream.Seek(0, SeekOrigin.Begin);
                    if (Path.GetExtension(filename).ToLower() == ".jpg") // load exif only for jpg
                    {
                        StringBuilder info = new StringBuilder();
                        info.AppendLine(filename + "\n" + (int)img.Width + "x" + (int)img.Height);
                        var decoder = new JpegBitmapDecoder(imgStream, BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                        var bitmapFrame = decoder.Frames[0];
                        if (bitmapFrame != null)
                        {
                            BitmapMetadata metaData = (BitmapMetadata)bitmapFrame.Metadata.Clone();
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
                            }
                        }
                        Overlay.Text = info.ToString();
                    }
                    else
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

		public static void Rotate90(string fileName)
		{
			Image Pic;
			string FileNameTemp;
			System.Drawing.Imaging.Encoder Enc = System.Drawing.Imaging.Encoder.Transformation;
			EncoderParameters EncParms = new EncoderParameters(1);
			EncoderParameter EncParm;
			ImageCodecInfo CodecInfo = GetEncoderInfo("image/jpeg");

			// load the image to change 
			Pic = Image.FromFile(fileName);

			Image Pic2 = (Image) Pic.Clone();

			// we cannot store in the same image, so use a temporary image instead 
			FileNameTemp = fileName + ".temp";

			// for rewriting without recompression we must rotate the image 90 degrees
			EncParm = new EncoderParameter(Enc, (long)EncoderValue.TransformRotate90);
			EncParms.Param[0] = EncParm;

			// now write the rotated image with new description 
			Pic.Save(FileNameTemp, CodecInfo, EncParms);
			Pic.Dispose();
			Pic = null;

			// delete the original file, will be replaced later 
			//System.IO.File.Delete(fileName);
			//System.IO.File.Move(FileNameTemp, fileName);
		}

		private static ImageCodecInfo GetEncoderInfo(String mimeType)
		{
			int j;
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			for (j = 0; j < encoders.Length; ++j)
			{
				if (encoders[j].MimeType == mimeType)
					return encoders[j];
			}
			return null;
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
