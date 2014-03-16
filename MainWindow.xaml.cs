
// -----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace FaceTrackingandVARecord
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using System.ComponentModel;
    
    using System.IO;
    using System.Threading;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;

    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public  partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();
        private WriteableBitmap colorImageWritableBitmap;
        private byte[] colorImageData;
        private ColorImageFormat currentColorImageFormat = ColorImageFormat.Undefined;
        //audio
        string _recordingFileName;
        
        MediaPlayer _mplayer;
        bool _isPlaying;
        bool _isNoiseSuppressionOn;
        bool _isAutomaticGainOn;
        bool _isAECOn;
        public Bitmap bmp;
        static public int vMode=0;
              
        public MainWindow()
        {
            InitializeComponent();
            
            var faceTrackingViewerBinding = new Binding("Kinect") { Source = sensorChooser };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.KinectProperty, faceTrackingViewerBinding);

            sensorChooser.KinectChanged += SensorChooserOnKinectChanged;

            sensorChooser.Start();

            this.Loaded += delegate { KinectSensor.KinectSensors[0].Start(); };
            _mplayer = new MediaPlayer();
            _mplayer.MediaEnded += delegate { _mplayer.Close(); IsPlaying = false; };
            this.DataContext = this;
            
        }
        //audio

        private void Play()
        {
            IsPlaying = true;
            _mplayer.Open(new Uri(_recordingFileName, UriKind.Relative));
            _mplayer.Play();
        }


        private void Record()
        {
            var time = DateTime.Now.ToString("hhmmss");
            RecorderHelper.auRecord = new StreamWriter(time+"AU.txt");
            if (VideoROn)
            {
                RecorderHelper.aviManager = new AviFile.AviManager(time + ".avi", false);
                RecorderHelper.aviStream = null;
                //Thread threadV = new Thread(new ThreadStart(RecordKinectVideo));                
                //threadV.Start();
            }

            Thread thread = new Thread(new ThreadStart(RecordKinectAudio));
            thread.Priority = ThreadPriority.Highest;
            thread.Start();


        }

        private void Stop()
        {
            if (VideoROn)
            {
                RecorderHelper.auRecord.Close();
                RecorderHelper.aviManager.Close();
            }
            KinectSensor.KinectSensors[0].AudioSource.Stop();
            IsRecording = false;
        }


        private void RecordKinectVideo()
        {

            //im=RecordVideo.GetDocument();                     
            /*byte[] pixeldata = new byte[im.PixelDataLength];
            im.CopyPixelDataTo(pixeldata);
            Bitmap bmap = new Bitmap(im.Width, im.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            BitmapData bmapdata = bmap.LockBits(
                new System.Drawing.Rectangle(0, 0, im.Width, im.Height),
                ImageLockMode.WriteOnly,
                bmap.PixelFormat);
            IntPtr ptr = bmapdata.Scan0;
            Marshal.Copy(pixeldata, 0, ptr, im.PixelDataLength);
            bmap.UnlockBits(bmapdata);*/
            //while (((RecorderHelper.IsRecording == true) && (RecorderHelper.VideoROn)) )
            //while (RecorderHelper.IsRecording == true)
            while (true)
            {
                //if (RecordVideo.IsDocumentAvailable && RecordVideo.rw.IsWriterLockHeld)
                
                    RecordVideo.rw.AcquireWriterLock(50);
                    if (RecordVideo.dataQueue.Count > 0 && RecordVideo.rw.IsWriterLockHeld)
                    {
                        ColorImageFrame im = null;
                        //im = RecordVideo.dataQueue.Dequeue();
                        im = RecordVideo.dataQueue.Peek();
                        Bitmap bmap = RecorderHelper.ImageToBitmap(im);
                        if (RecorderHelper.aviStream == null)
                        {
                            RecorderHelper.aviStream = RecorderHelper.aviManager.AddVideoStream(false, 24, bmap);
                        }
                        else
                        {
                            RecorderHelper.aviStream.AddFrame(bmap);
                        }
                        RecordVideo.dataQueue.Dequeue();
                        //bm.Dispose();
                    }
                    RecordVideo.rw.ReleaseWriterLock();
                }//while                
            
        }

        private object lockObj = new object();
        private void RecordKinectAudio()
        {
            lock (lockObj)
            {
                IsRecording = true;
                
                
                var source = CreateAudioSource();

                var time = DateTime.Now.ToString("hhmmss");
                _recordingFileName = time + ".wav";
                using (var fileStream =
                new FileStream(_recordingFileName, FileMode.Create))
                {
                    RecorderHelper.WriteWavFile(source, fileStream);
                }
                
                IsRecording = false;
                
            }

        }

        private KinectAudioSource CreateAudioSource()
        {
            var source = KinectSensor.KinectSensors[0].AudioSource;
            source.BeamAngleMode = BeamAngleMode.Adaptive;
            source.NoiseSuppression = _isNoiseSuppressionOn;
            source.AutomaticGainControlEnabled = _isAutomaticGainOn;

            if (IsAECOn)
            {
                source.EchoCancellationMode = EchoCancellationMode.CancellationOnly;
                source.AutomaticGainControlEnabled = false;
                IsAutomaticGainOn = false;
                source.EchoCancellationSpeakerIndex = 0;
            }

            return source;
        }
        //audio


        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
            KinectSensor newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= KinectSensorOnAllFramesReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }

            if (newSensor != null)
            {
                try
                {
                    newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    newSensor.SkeletonStream.Enable();
                    newSensor.AllFramesReady += KinectSensorOnAllFramesReady;
                }
                catch (InvalidOperationException)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur, say, in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                }
            }
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            sensorChooser.Stop();
            faceTrackingViewer.Dispose();
        }

        private void KinectSensorOnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            using (var colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame())
            {
                if (colorImageFrame == null)
                {
                    return;
                }
                //Console.WriteLine("WHAT is that");
                // Make a copy of the color frame for displaying.
                var haveNewFormat = this.currentColorImageFormat != colorImageFrame.Format;
                if (haveNewFormat)
                {
                    this.currentColorImageFormat = colorImageFrame.Format;
                    this.colorImageData = new byte[colorImageFrame.PixelDataLength];
                    this.colorImageWritableBitmap = new WriteableBitmap(
                        colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                    ColorImage.Source = this.colorImageWritableBitmap;
                }
                
                //Record
                /*if ((RecorderHelper.IsRecording) && (VideoROn))
                {
                    RecordVideo.AddDocument(colorImageFrame);
                }*/
                //DRAW the image                
                colorImageFrame.CopyPixelDataTo(this.colorImageData);
                this.colorImageWritableBitmap.WritePixels(
                    new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height),
                    this.colorImageData,
                    colorImageFrame.Width * Bgr32BytesPerPixel,
                    0);
            }
        }

        //audio
        #region user interaction handlers

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            Record();
        }


        private void button3_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        #endregion
        //audio
        #region properties

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

       

        private bool IsPlaying
        {
            get
            {
                return _isPlaying;
            }
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged("IsRecordingEnabled");
                }
            }
        }

        private bool IsRecording
        {
            get
            {
                return RecorderHelper.IsRecording;
            }
            set
            {
                if (RecorderHelper.IsRecording != value)
                {
                    RecorderHelper.IsRecording = value;
                    OnPropertyChanged("IsPlayingEnabled");
                    OnPropertyChanged("IsRecordingEnabled");
                    OnPropertyChanged("IsStopEnabled");
                }
            }
        }
        public bool IsPlayingEnabled
        {
            get { return !IsRecording; }
        }

        public bool IsRecordingEnabled
        {
            get { return !IsPlaying && !IsRecording; }
        }

        public bool IsStopEnabled
        {
            get { return IsRecording; }
        }


        public bool IsNoiseSuppressionOn
        {
            get
            {
                return _isNoiseSuppressionOn;
            }
            set
            {
                if (_isNoiseSuppressionOn != value)
                {
                    _isNoiseSuppressionOn = value;
                    OnPropertyChanged("IsNoiseSuppressionOn");
                }
            }
        }

        public bool IsAutomaticGainOn
        {
            get
            {
                return _isAutomaticGainOn;
            }
            set
            {
                if (_isAutomaticGainOn != value)
                {
                    _isAutomaticGainOn = value;
                    OnPropertyChanged("IsAutomaticGainOn");
                }
            }
        }


        public bool IsAECOn
        {
            get
            {
                return _isAECOn;
            }
            set
            {
                if (_isAECOn != value)
                {
                    _isAECOn = value;
                    OnPropertyChanged("IsAECOn");
                }
            }
        }

        private bool VideoROn
    {
        get
        {
            return RecorderHelper.VideoROn;
        }
        set
        {
            if (RecorderHelper.VideoROn != value)
            {
                RecorderHelper.VideoROn = value;
                OnPropertyChanged("VideoROn");
            }
        }
    }
        
        #endregion
        //audio
        private void RadioButton_Checked_1(object sender, RoutedEventArgs e)
        {
            vMode = 1;
        }

        private void RadioButton_Checked_2(object sender, RoutedEventArgs e)
        {
            vMode = 2;
        }

        private void RadioButton_Checked_3(object sender, RoutedEventArgs e)
        {
            vMode = 0;
        }

        private void CheckBox_Checked_1(object sender, RoutedEventArgs e)
        {
            VideoROn = true;
        }

        private void CheckBox_Unchecked_1(object sender, RoutedEventArgs e)
        {
            VideoROn = false;
        }


    }
}
