using System;
using System.IO;
using System.Drawing;
using GrapeCity.Documents.Drawing;
using GrapeCity.Documents.Text;
using GrapeCity.Documents.Imaging;
namespace AWSLambdaThumbnail
{
    public class GcImagingOperations
    {
        public static MemoryStream GetConvertedImage(byte[] stream)
        {
            using (var bmp = new GcBitmap())
            {
                bmp.Load(stream);               
                //  Resize to thumbnail 
                var resizedImage = bmp.Resize(100, 100);
                MemoryStream m = new MemoryStream();
                resizedImage.SaveAsJpeg(m);
                return m;                
            }
        }
    }
}