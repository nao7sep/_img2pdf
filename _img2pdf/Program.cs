﻿using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace _img2pdf
{
    class Program
    {
        static void Main (string [] args)
        {
            try
            {
                // In compliance with the AGPL license, the following message must be displayed.
                Console.WriteLine ("Project Page: https://github.com/nao7sep/_img2pdf");

                if (args.Length == 0)
                {
                    Console.WriteLine ("Usage: _img2pdf.exe <image1> <image2> ...");
                    return;
                }

                string [] xSupportedExtensions = [ ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" ];

                foreach (string xSourceDirectoryPath in args)
                {
                    if (Directory.Exists (xSourceDirectoryPath) == false)
                        throw new DirectoryNotFoundException (xSourceDirectoryPath);

                    if (Directory.GetFiles (xSourceDirectoryPath, "*.*", SearchOption.TopDirectoryOnly).
                            Count (x => xSupportedExtensions.Contains (System.IO.Path.GetExtension (x).ToLower ())) < 2)
                        throw new ArgumentException ("Contains less than two images: " + xSourceDirectoryPath);
                }

                int? xOriginalResolution;

                while (true)
                {
                    Console.Write ("Resolution of original images (DPI): ");
                    string? xOriginalResolutionString = Console.ReadLine ();

                    if (int.TryParse (xOriginalResolutionString, out int xValue) && xValue > 0)
                    {
                        xOriginalResolution = xValue;
                        break;
                    }
                }

                int? xResizeFactor;

                while (true)
                {
                    Console.Write ("Divide image dimensions by: ");
                    string? xResizeFactorString = Console.ReadLine ();

                    if (int.TryParse (xResizeFactorString, out int xValue) && xValue > 0)
                    {
                        xResizeFactor = xValue;
                        break;
                    }
                }

                double xNewResolution = (double) xOriginalResolution / xResizeFactor.Value;

                // Very limited information is found about the compression settings of iText7.
                // These shouldnt be negatively affecting anything.
                // SetFullCompressionMode => Defines if full compression mode is enabled. If enabled, not only the content of the pdf document will be compressed, but also the pdf document inner structure.
                // SetCompressionLevel => Defines the level of compression for the document.
                var xWriterProperties = new WriterProperties ().SetFullCompressionMode (true).SetCompressionLevel (CompressionConstants.BEST_COMPRESSION);

                foreach (string xSourceDirectoryPath in args)
                {
                    try
                    {
                        string xPdfFilePath = System.IO.Path.ChangeExtension (xSourceDirectoryPath, ".pdf");

                        using var xPdfWriter = new PdfWriter (xPdfFilePath, xWriterProperties);
                        using var xPdfDocument = new PdfDocument (xPdfWriter);
                        using var xDocument = new Document (xPdfDocument);

                        // In Windows Explorer, files are sorted in a natural order, meaning the extensions are included, numbers are parsed and the punctuation marks are treated uniquely.
                        // In the ASCII table, characters such as ' ', '(' and '-' come before '.'.
                        // If extensions are included, "file.jpg" may come after "file (1).jpg".
                        // But if we exclude them, files may be sorted differently than they are in other apps, where we work with files.
                        // The best practice should be to make sure that '_' is used to attach numbers to file names so that '.' will come before it.

                        string [] xImageFilePaths = Directory.GetFiles (xSourceDirectoryPath, "*.*", SearchOption.TopDirectoryOnly).
                            Where (x => xSupportedExtensions.Contains (System.IO.Path.GetExtension (x).ToLower ())).Order (StringComparer.OrdinalIgnoreCase).ToArray ();

                        for (int temp = 0; temp < xImageFilePaths.Length; temp ++)
                        {
                            using var xOriginalImage = SixLabors.ImageSharp.Image.Load <Rgba32> (xImageFilePaths [temp]);

                            int xOriginalWidth = xOriginalImage.Width,
                                xOriginalHeight = xOriginalImage.Height;

                            int xNewWidth = (int) Math.Round ((double) xOriginalWidth / xResizeFactor.Value),
                                xNewHeight = (int) Math.Round ((double) xOriginalHeight / xResizeFactor.Value);

                            float xNewWidthInPoints = (float) (xNewWidth * 72f / xNewResolution),
                                  xNewHeightInPoints = (float) (xNewHeight * 72f / xNewResolution);

                            xOriginalImage.Mutate (x => x.Resize (xNewWidth, xNewHeight));

                            // ImageSharp can strip metadata from images, but let's be extra cautious.
                            // We dont make the same PDF files repeatedly, but the files are sent to many people.

                            using var xNewImage = new SixLabors.ImageSharp.Image <Rgba32> (xNewWidth, xNewHeight);
                            xNewImage.Mutate (x => x.DrawImage (xOriginalImage, opacity: 1));

                            using var xMemoryStream = new MemoryStream ();
                            var xEncoder = new JpegEncoder { Quality = 75, SkipMetadata = true };
                            xNewImage.Save (xMemoryStream, xEncoder);
                            // xMemoryStream.Position = 0; >= Doesnt seem to be necessary.

                            var xImageData = ImageDataFactory.Create (xMemoryStream.ToArray ());
                            var xImage = new Image (xImageData);
                            xImage.SetFixedPosition (left: 0, bottom: 0);
                            xImage.ScaleToFit (xNewWidthInPoints, xNewHeightInPoints);

                            xPdfDocument.SetDefaultPageSize (new PageSize (xNewWidthInPoints, xNewHeightInPoints));

                            if (temp > 0)
                                xDocument.Add (new AreaBreak (AreaBreakType.NEXT_PAGE));

                            xDocument.Add (xImage);

                            Console.Write ($"\rImage {temp + 1} of {xImageFilePaths.Length} added.");
                        }

                        Console.WriteLine ();
                        Console.WriteLine ("PDF file created: " + xPdfFilePath);
                    }

                    catch (Exception xException)
                    {
                        // Resizing images and generating PDF files are time-consuming tasks.
                        // If something wrong happens, the program should continue with the next directory.

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine (xException.ToString ());
                        Console.ResetColor ();
                    }
                }
            }

            catch (Exception xException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine (xException.ToString ());
                Console.ResetColor ();
            }

            finally
            {
                Console.Write ("Press any key to exit: ");
                Console.ReadKey (true);
                Console.WriteLine ();
            }
        }
    }
}
