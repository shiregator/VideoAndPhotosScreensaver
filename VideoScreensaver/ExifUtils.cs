using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VideoScreensaver
{
    class ExifUtils
    {
        private BitmapMetadata _metaData = null;
        private int _width;
        private int _height;
        private JpegBitmapDecoder decoder; 


        public ExifUtils()
        {

        }

        public bool ReadExifFromFile(String filename)
        {
            try
            {
                return ReadExifFromFile(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            } catch
            {
                return false;
            }
        }

        public bool ReadExifFromFile(Stream stream)
        {
            try
            {
                decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var bitmapFrame = decoder.Frames[0];

                if (bitmapFrame != null)
                {
                    _width = (int)bitmapFrame.Width;
                    _height = (int)bitmapFrame.Height;
                    _metaData = (BitmapMetadata)bitmapFrame.Metadata.Clone();
                    stream.Close();
                } else
                {
                    return false;
                }
                return true;
            } catch
            {
                return false;
            }
        }

        public string GetInfoString()
        {
            if (_metaData == null)
                return "";
            StringBuilder info = new StringBuilder();            
            info.AppendLine(_width + "x" + _height);
            if (!String.IsNullOrWhiteSpace(_metaData.DateTaken))
            {
                info.AppendLine("Date taken: " + _metaData.DateTaken);
            }
            if (!String.IsNullOrWhiteSpace(_metaData.Title))
            {
                info.AppendLine("Title: " + _metaData.Title);
            }
            if (!String.IsNullOrWhiteSpace(_metaData.Subject))
            {
                info.AppendLine("Subject: " + _metaData.Subject);
            }
            if (!String.IsNullOrWhiteSpace(_metaData.Comment))
            {
                info.AppendLine("User comment: " + _metaData.Comment);
            }

            PrintMetadata(string.Empty);
            String xmpSubject = (String)_metaData.GetQuery("/xmp/dc:subject/{ulong=0}");
            if (!String.IsNullOrWhiteSpace(xmpSubject))
            {
                info.Append("Subject: " + xmpSubject);
                int i = 1;
                while (!String.IsNullOrWhiteSpace(xmpSubject))
                {
                    xmpSubject = (String)_metaData.GetQuery("/xmp/dc:subject/{ulong=" + i + "}");
                    if (!String.IsNullOrWhiteSpace(xmpSubject))
                        info.Append(", " + xmpSubject);
                    i++;
                }
                info.AppendLine();
            }

            object keywords = _metaData.GetQuery("/app13/{ushort=0}/{ulonglong=61857348781060}/iptc/{str=Keywords}");
            if (keywords != null)
            {
                if (keywords.GetType() == typeof(string))
                    info.AppendLine("Keywords: " + keywords);
                else if ((keywords.GetType() == typeof(string[])))
                {
                    if (((string[])keywords).Length > 0)
                    {
                        info.Append("Keywords: " + ((string[])keywords)[0]);
                        for (int i = 1; i < ((string[])keywords).Length; i++)
                        {
                            info.Append(", " + ((string[])keywords)[i]);
                        }
                        info.AppendLine();
                    }
                }
            }
            return info.ToString();
        }

        public ushort GetOrientation()
        {
            if (_metaData.ContainsQuery(@"/app1/{ushort=0}/{ushort=274}"))
            {
                return (UInt16)_metaData.GetQuery(@"/app1/{ushort=0}/{ushort=274}"); //get rotation
            }
            else
            {
                return 1;
            }
        }

        /// <summary>
        /// Return the proper System.Drawing.RotateFlipType according to given orientation EXIF metadata
        /// </summary>
        /// <param name="orientation">Exif "Orientation"</param>
        /// <returns>the corresponding System.Drawing.RotateFlipType enum value</returns>
        public static System.Drawing.RotateFlipType GetRotateFlipTypeByExifOrientationData(ushort orientation)
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

        public static int GetBitmapRotationAngleByRotationFlipType(System.Drawing.RotateFlipType rotation)
        {
            switch (rotation)
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

        public static int GetNextRotationOrientation(int currentOrientation)
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
                    return currentOrientation;
            }
        }


        public void PrintMetadata(string fullQuery)
        {
            PrintMetadataRecursive(fullQuery, _metaData);
        }

        private void PrintMetadataRecursive(string fullQuery, System.Windows.Media.ImageMetadata metadata)
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
                        PrintMetadataRecursive(tempQuery, moreMetadata);
                    }
                }
            }
        }

        public static UInt16 RotateImageViaInPlaceBitmapMetadataWriter(string filename, UInt16 prevOrient)
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
                throw new ArgumentException("The file you passed in is not a JPEG:\n " + filename, "filename");
            }

            // This code is based on http://blogs.msdn.com/b/rwlodarc/archive/2007/07/18/using-wpf-s-inplacebitmapmetadatawriter.aspx
            using (Stream originalFile = File.Open(filename, FileMode.Open, FileAccess.ReadWrite))
            {
                ConsoleColor originalColor = Console.ForegroundColor;

                BitmapDecoder output = BitmapDecoder.Create(originalFile, BitmapCreateOptions.None, BitmapCacheOption.Default);

                InPlaceBitmapMetadataWriter metadata = output.Frames[0].CreateInPlaceBitmapMetadataWriter();

                // Within the InPlaceBitmapMetadataWriter, you can add, update, or remove metadata.
                if (!metadata.ContainsQuery("/app1"))
                    metadata.SetQuery("/app1", new BitmapMetadata("app1"));
                if (!metadata.ContainsQuery("/app1/{ushort=0}"))
                    metadata.SetQuery("/app1/{ushort=0}", new BitmapMetadata("ifd"));
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
                    throw new Exception("Rotate Image failed. Try to 'S' (Show in Folder) and manually rotate it.");
                }

                Console.ForegroundColor = originalColor;

                return orient;
            }

        }

        public static UInt16 RotateImageViaTranscoding(string filename, UInt16 prevOrient)
        {
            UInt16 orient = 1;
            orient = (UInt16)GetNextRotationOrientation(prevOrient); // get new rotation

            // This only works for jpg photos
            if (!filename.EndsWith("jpg"))
            {
                Console.WriteLine("The file you passed in is not a JPEG.");
				throw new ArgumentException("The file you passed in is not a JPEG:\n " + filename, "filename");
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
                    if (!metadata.ContainsQuery("/app1"))
                        metadata.SetQuery("/app1", new BitmapMetadata("app1"));
                    if (!metadata.ContainsQuery("/app1/{ushort=0}"))
                        metadata.SetQuery("/app1/{ushort=0}", new BitmapMetadata("ifd"));
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


    }
}
