using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace RollPitchYaw.ViewModels
{
    internal class Spindle3DViewModel : ObservableObject
    {
        private double _roll; 
        public double Roll
        {
            get => _roll;
            set
            {
                SetProperty(ref _roll, value);
            }
        }

        private double _pitch;
        public double Pitch
        {
            get => _pitch;
            set
            {
                SetProperty(ref _pitch, value);
            }
        }

        private double _yaw;
        public double Yaw
        {
            get => _yaw;
            set
            {
                SetProperty(ref _yaw, value);
            }
        }

        private double _thickness = 0.4;
        public double Thickness
        {
            get => _thickness;
            set
            {
                SetProperty(ref _thickness, value);
            }
        }

        public RelayCommand<string> SpindleCommand { get; set; }

        public Spindle3DViewModel()
        {
            SpindleCommand = new RelayCommand<string>(SpindleCommand_Function);

            _rotateTimer = new DispatcherTimer();
            _rotateTimer.Interval = TimeSpan.FromMilliseconds(10);
            _rotateTimer.Tick += rotateTimer_Tick;

            _spindleTimer = new DispatcherTimer();
            _spindleTimer.Interval = TimeSpan.FromMilliseconds(10);
            _spindleTimer.Tick += spindleTimer_Tick;
        }

        DispatcherTimer _rotateTimer;
        DispatcherTimer _spindleTimer;
        bool _spindleStart = true;
        double MOVE_UNIT = 0.5;
        double START_LIMIT = -90;
        double STOP_LIMIT = 0;

        void SpindleCommand_Function(string para)
        {
            _spindleTimer.Stop();
            switch (para.ToLower())
            {
                case "start":
                    _spindleStart = true;
                    break;

                case "stop":
                    _spindleStart = false;
                    break;
            }
            _rotateTimer.Start();
            _spindleTimer.Start();
        }

        void rotateTimer_Tick(object sender, EventArgs e)
        {
            Roll = (Roll + 4) % 360;
        }

        void spindleTimer_Tick(object sender, EventArgs e)
        {
            bool completed = false;

            if (_spindleStart)
            {
                if (Pitch > START_LIMIT)
                {
                    completed = false;
                    Pitch -= MOVE_UNIT;
                }
                else
                {
                    completed = true;
                    Pitch = START_LIMIT;
                }

                if (Yaw > START_LIMIT)
                {
                    completed = false;
                    Yaw -= MOVE_UNIT;
                }
                else
                {
                    completed = true;
                    Yaw = START_LIMIT;
                }

            }
            else
            {
                if (Pitch < STOP_LIMIT)
                {
                    completed = false;
                    Pitch += MOVE_UNIT;
                }
                else
                {
                    completed = true;
                    Pitch = STOP_LIMIT;
                }

                if (Yaw < STOP_LIMIT)
                {
                    completed = false;
                    Yaw += MOVE_UNIT;
                }
                else
                {
                    completed = true;
                    Yaw = STOP_LIMIT;
                }
            }

            if (completed)
            {
                if (_spindleStart)
                {
                    _spindleTimer.Stop();
                }
                else
                {
                    Roll = 0;
                    _rotateTimer.Stop();
                    _spindleTimer.Stop();
                }
            }
        }

    }

}
