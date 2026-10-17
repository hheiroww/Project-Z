using System.Windows;

namespace AppearanceSamples
{
    public partial class Demo : Window
    {
        public Demo()
        {
            InitializeComponent();
        }

        public void UpdateCorners()
        {
            Curve.CornerRadius = new CornerRadius(30, 20, 10, 0);
        }
    }
}