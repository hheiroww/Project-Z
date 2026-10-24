
namespace ProjectZ.Shared.Content {
    public partial class Fonts {
        public class ProjectZSans {
            public static string get_GetResourceName(int FontSize) => "ProjectZSans_" + FontSize;
        }

        public class SegoeUI {

            public static string get_GetResourceName(int FontSize) {
                return ProjectZSans.get_GetResourceName(FontSize);
            }

        }

    }

}
