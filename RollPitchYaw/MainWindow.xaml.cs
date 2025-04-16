using RollPitchYaw.ViewModels;
using RollPitchYaw.Views;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Controls;

namespace RollPitchYaw
{
    public partial class MainWindow : Window
    {
        private Spindle3DViewModel _viewModel;
        private SpindleAssy _spindleVisualInstance;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = DataContext as Spindle3DViewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            _spindleVisualInstance = new SpindleAssy
            {
                Width = 200,
                Height = 200
            };

            _spindleVisualInstance.Measure(new Size(200, 200));
            _spindleVisualInstance.Arrange(new Rect(0, 0, 200, 200));
            _spindleVisualInstance.UpdateLayout(); // (선택) 레이아웃 완성 강제

            Build3DModel();
        }

        private ImageBrush GenerateImageBrushFromSpindleAssy()
        {
            var bmp = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(_spindleVisualInstance);

            return new ImageBrush(bmp)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }

        private GeometryModel3D CreateRoundPlane(double radius, double z, ImageBrush brush, int segments = 64)
        {
            var mesh = new MeshGeometry3D();

            mesh.Positions.Add(new Point3D(0, 0, z)); // 중심점

            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                mesh.Positions.Add(new Point3D(x, y, z));
            }

            for (int i = 1; i <= segments; i++)
            {
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(i);
                mesh.TriangleIndices.Add(i + 1);
            }

            // UV 좌표 설정 (정규화된 중심 기준)
            mesh.TextureCoordinates.Add(new Point(0.5, 0.5)); // 중심
            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double u = 0.5 + 0.5 * Math.Cos(angle);
                double v = 0.5 - 0.5 * Math.Sin(angle);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(brush)
            };
        }


        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.Thickness))
                Build3DModel();
        }

        private void Build3DModel()
        {
            MyModelGroup.Children.Clear();

            double radius = 1.0;
            double thickness = _viewModel.Thickness;

            // 윗면
            var frontBrush = GenerateCircularMaskedImageBrush(_spindleVisualInstance, 200, 200);
            var front = CreateRoundPlane(radius, 0, frontBrush); // ✅ Z=0
            MyModelGroup.Children.Add(front);

            // 아랫면
            var back = CreatePlane(z: -thickness, isBack: true); // ✅ Z=-thickness
            MyModelGroup.Children.Add(back);

            // 측면
            var side = CreateRingSide(64, radius, thickness);    // 내부에서 Z 방향 반영됨
            MyModelGroup.Children.Add(side);

            // 눈금선
            for (int i = 0; i < 360; i += 10)
            {
                var line = CreateLineOnSide(i, radius, thickness);
                MyModelGroup.Children.Add(line);
            }
        }

        private GeometryModel3D CreatePlane(double z, bool isBack)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
        {
            new Point3D(-1, 1, z),
            new Point3D(1, 1, z),
            new Point3D(1, -1, z),
            new Point3D(-1, -1, z)
        },
                TriangleIndices = isBack
                    ? new Int32Collection { 0, 3, 2, 0, 2, 1 }
                    : new Int32Collection { 0, 1, 2, 0, 2, 3 },
                TextureCoordinates = new PointCollection
        {
            new Point(0, 0),
            new Point(1, 0),
            new Point(1, 1),
            new Point(0, 1)
        }
            };

            //var brush = GenerateImageBrushFromSpindleAssy(); // ✅ 이미지로 렌더링된 브러시
            var brush = GenerateCircularMaskedImageBrush(_spindleVisualInstance, 200, 200);

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(brush)
            };
        }

        private ImageBrush GenerateCircularMaskedImageBrush(UIElement visual, int width, int height)
        {
            // 1. Render original visual to bitmap
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // 2. Create circular alpha mask
            var mask = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

            int radius = width / 2;
            var center = new Point(radius, radius);

            // 3. Apply alpha mask
            mask.Lock();
            unsafe
            {
                IntPtr pBackBuffer = mask.BackBuffer;
                int stride = mask.BackBufferStride;
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        double dx = x - center.X;
                        double dy = y - center.Y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);

                        byte alpha = dist < radius ? (byte)255 : (byte)0;

                        int index = x * 4;
                        row[index + 0] = 255;       // B
                        row[index + 1] = 255;       // G
                        row[index + 2] = 255;       // R
                        row[index + 3] = alpha;     // A
                    }
                }
            }
            mask.AddDirtyRect(new Int32Rect(0, 0, width, height));
            mask.Unlock();

            // 4. Combine: use OpacityMask
            var visualImage = new Image
            {
                Width = width,
                Height = height,
                Source = rtb,
                OpacityMask = new ImageBrush(mask)
            };

            // 5. Render final circular masked image
            var final = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            visualImage.Measure(new Size(width, height));
            visualImage.Arrange(new Rect(0, 0, width, height));
            final.Render(visualImage);

            return new ImageBrush(final)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }


        private GeometryModel3D CreateRingSide(int segments, double radius, double thickness)
        {
            double zFront = 0;
            double zBack = -thickness;

            var mesh = new MeshGeometry3D();

            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);

                mesh.Positions.Add(new Point3D(x, y, zFront)); // 앞
                mesh.Positions.Add(new Point3D(x, y, zBack));  // 뒤
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = i * 2 + 2;
                int i3 = i * 2 + 3;

                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i3);
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i3);
                mesh.TriangleIndices.Add(i2);
            }

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray))
            };
        }

        private GeometryModel3D CreateRingSide(int segments, double radius, double thickness, double zBase = 0)
        {
            double zFront = zBase + thickness;  // 위
            double zBack = zBase;               // 아래

            var mesh = new MeshGeometry3D();

            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);

                mesh.Positions.Add(new Point3D(x, y, zFront));
                mesh.Positions.Add(new Point3D(x, y, zBack));
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = i * 2 + 2;
                int i3 = i * 2 + 3;

                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i3);
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i3);
                mesh.TriangleIndices.Add(i2);
            }

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray))
            };
        }

        GeometryModel3D CreateLineOnSide(double angleDeg, double radius, double thickness)
        {
            double angle = angleDeg * Math.PI / 180;
            double lineWidth = 0.01;

            double x = radius * Math.Cos(angle);
            double y = radius * Math.Sin(angle);

            // 수직 방향의 선을 만드는 평면
            Vector3D normal = new Vector3D(-y, x, 0); // 수직 방향
            normal.Normalize();
            normal *= lineWidth;

            Point3D p1 = new Point3D(x + normal.X, y + normal.Y, 0);
            Point3D p2 = new Point3D(x - normal.X, y - normal.Y, 0);
            Point3D p3 = new Point3D(x - normal.X, y - normal.Y, -thickness);
            Point3D p4 = new Point3D(x + normal.X, y + normal.Y, -thickness);

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { p1, p2, p3, p4 },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
            };

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Black))
            };
        }

    }
}
