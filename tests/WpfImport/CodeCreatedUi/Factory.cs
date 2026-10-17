using System.Windows;

namespace CodeCreatedUi
{
    public static class Factory
    {
        public static Window Create()
        {
            return new Window
            {
                Title = "Code-created native window",
                Width = 320,
                Height = 200
            };
        }
    }
}