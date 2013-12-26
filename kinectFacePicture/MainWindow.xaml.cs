using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.FaceTracking;
using System.Globalization;
using System.IO;


namespace kinectFacePicture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        KinectSensor sensor;
        FaceTracker faceTracker;
        byte[] colorPixelData;
        short[] depthPixelData;
        Skeleton[] skeletonData;
        WriteableBitmap headImage;
        ColorImageFrame colorImageFrame;
        CroppedBitmap croppedImage;
        BitmapSource fullBitmap;



        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

            if (KinectSensor.KinectSensors.Count > 0)
            {
                sensor = KinectSensor.KinectSensors[0];
                


                if (sensor.Status == KinectStatus.Connected)
                {
                        sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                        sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                         
                        sensor.DepthStream.Range = DepthRange.Default;
                        sensor.SkeletonStream.EnableTrackingInNearRange = false;
                        
                        sensor.SkeletonStream.Enable();
                        sensor.AllFramesReady += sensor_AllFramesReady;
                    
                        sensor.Start();
                    
                    

                   
                }

            }

        }

        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {

            colorPixelData = new byte[sensor.ColorStream.FramePixelDataLength];
            depthPixelData = new short[sensor.DepthStream.FramePixelDataLength];
            skeletonData = new Skeleton[6];

            if (this.faceTracker == null)
            {
                try
                {
                    this.faceTracker = new FaceTracker(sensor);
                }
                catch (InvalidOperationException)
                {
                    // During some shutdown scenarios the FaceTracker
                    // is unable to be instantiated.  Catch that exception
                    // and don't track a face.
                    System.Diagnostics.Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                    this.faceTracker = null;
                }
            }


            using ( colorImageFrame = e.OpenColorImageFrame())
            {
                if (colorImageFrame == null)
                    return;
                colorImageFrame.CopyPixelDataTo(colorPixelData);
                fullBitmap = BitmapSource.Create(colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null, colorPixelData, (colorImageFrame.Width*4));
                Image1.Source = fullBitmap;            
            }

            using (DepthImageFrame depthImageFrame = e.OpenDepthImageFrame())
            {
                if (depthImageFrame == null)
                    return;
                depthImageFrame.CopyPixelDataTo(depthPixelData);
            }

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                    return;
                skeletonFrame.CopySkeletonDataTo(skeletonData);
            }
            var skeleton = skeletonData.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);
            if (skeleton == null)
                return;
            
            if (this.faceTracker != null)
            {
                FaceTrackFrame faceframe = faceTracker.Track(sensor.ColorStream.Format, colorPixelData, sensor.DepthStream.Format, depthPixelData, skeleton);
                if (faceframe.TrackSuccessful == true)
                {
                    headTracked.Text = ("now I see you!");
                    System.Diagnostics.Debug.WriteLine("tracked");
                    headImage = new WriteableBitmap(colorImageFrame.Width, colorImageFrame.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                    Int32Rect cropRect = new Int32Rect((int)(faceframe.FaceRect.Left), (int)(faceframe.FaceRect.Top), (int)(faceframe.FaceRect.Width*0.9), (int)(faceframe.FaceRect.Height*0.9));
                    headImage.WritePixels(new Int32Rect(faceframe.FaceRect.Left, faceframe.FaceRect.Top, faceframe.FaceRect.Width, faceframe.FaceRect.Height), colorPixelData, (int)headImage.Width * colorImageFrame.BytesPerPixel, 0);
                    croppedImage = new CroppedBitmap(fullBitmap, cropRect);
                    image2.Source = croppedImage;
                }

                
            }
        }

        private void WindowClosed(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stopKinect(sensor);

        }

        void stopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                sensor.Stop();
                sensor.AudioSource.Stop();
            }
        }


        private void takeShotClick(object sender, RoutedEventArgs e)
        {
            int colorWidth = croppedImage.PixelWidth;
            int colorHeight = croppedImage.PixelHeight;

            var renderBitmap = new RenderTargetBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Pbgra32);

            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {
                var colorBrush = new VisualBrush(image2);
                dc.DrawRectangle(colorBrush, null, new System.Windows.Rect(new System.Windows.Point(), new Size(colorWidth, colorHeight)));
            }
            renderBitmap.Render(dv);

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            var time = DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            var myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            var subPath = System.IO.Path.Combine(myPhotos, "subfolder");

            System.IO.Directory.CreateDirectory(subPath);

            var path = System.IO.Path.Combine(subPath, "KinectSnapshot-" + time + ".png");

            // write the new file to disk
            try
            {
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }
                System.Diagnostics.Debug.WriteLine("Lookin sexy baby!");
                
            }
            catch (System.IO.IOException)
            {
                System.Diagnostics.Debug.WriteLine("No picture taken");
                
            }
        }
    }
}
