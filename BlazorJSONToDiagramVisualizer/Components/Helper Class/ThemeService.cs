namespace BlazorJSONToDiagramVisualizer.Components.Helper_Class
{
    public class ThemeService
    {
        public event Action OnThemeChanged;

        public string CurrentTheme { get; private set; } = "light"; // "light" or "dark"

        public ThemeSettings CurrentThemeSettings { get; set; }

        public void SetTheme(string theme, ThemeSettings themeSettings)
        {
            CurrentTheme = theme;
            CurrentThemeSettings = themeSettings;
            OnThemeChanged?.Invoke();
        }
    }
    public class ThemeSettings
    {
        public string CssUrl { get; set; }
        public string DiagramBackgroundColor { get; set; }
        public string GridlinesColor { get; set; }
        public string HighlightColor { get; set; }
        public string NodeFillColor { get; set; }
        public string NodeStrokeColor { get; set; }
        public string TextKeyColor { get; set; }
        public string TextValueColor { get; set; }
        public string TextValueNullColor { get; set; }
        public string ExpandIconFillColor { get; set; }
        public string ExpandIconColor { get; set; }
        public string ExpandIconBorder { get; set; }
        public string ConnectorStrokeColor { get; set; }
        public string ChildCountColor { get; set; }
        public string BooleanColor { get; set; }
        public string NumericColor { get; set; }
        public string PopupKeyColor { get; set; }
        public string PopupValueColor { get; set; }
        public string PopupContentBGColor { get; set; }

        public ThemeSettings(string theme = "light")
        {
            if (theme == "light")
            {
                CssUrl = "https://cdn.syncfusion.com/blazor/29.1.33/styles/tailwind.css";
                DiagramBackgroundColor = "#F8F9FA";
                GridlinesColor = "#EBE8E8";
                HighlightColor = "#e7f0e6";
                NodeFillColor = "rgb(255, 255, 255)";
                NodeStrokeColor = "rgb(188, 190, 192)";
                TextKeyColor = "#A020F0";
                TextValueColor = "rgb(83, 83, 83)";
                TextValueNullColor = "rgb(41, 41, 41)";
                ExpandIconFillColor = "#e0dede";
                ExpandIconColor = " rgb(46, 51, 56)";
                ExpandIconBorder = "rgb(188, 190, 192)";
                ConnectorStrokeColor = "rgb(188, 190, 192)";
                ChildCountColor = "rgb(41, 41, 41)";
                BooleanColor = "rgb(74, 145, 67)";
                NumericColor = "rgb(182, 60, 30)";
                PopupKeyColor = "#5C940D";
                PopupValueColor = "#1864AB";
                PopupContentBGColor = "#F8F9FA";
            }
            else
            {
                CssUrl = "https://cdn.syncfusion.com/blazor/29.1.33/styles/tailwind-dark.css";
                DiagramBackgroundColor = "#1e1e1e";
                GridlinesColor = "rgb(40, 40, 40)";
                HighlightColor = "rgb(32, 97, 51, 0.5)";
                NodeFillColor = "rgb(41, 41, 41)";
                NodeStrokeColor = "rgb(66, 66, 66)";
                TextKeyColor = "#4dabf7";
                TextValueColor = "rgb(207, 227, 225)";
                TextValueNullColor = "rgb(151, 150, 149)";
                ExpandIconFillColor = "#1e1e1e";
                ExpandIconBorder = "rgb(66, 66, 66)";
                ExpandIconColor = "rgb(220, 221, 222)";
                ConnectorStrokeColor = "rgb(66, 66, 66)";
                ChildCountColor = "rgb(255, 255, 255)";
                BooleanColor = "rgb(61, 226, 49)";
                NumericColor = "rgb(232, 196, 121)";
                PopupKeyColor = "#A5D8FF";
                PopupValueColor = "#40C057";
                PopupContentBGColor = "#1A1A1A";
            }
        }
    }

}
