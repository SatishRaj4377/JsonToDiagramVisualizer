using BlazorJSONToDiagramVisualizer.Components.Helper_Class;

namespace BlazorJSONToDiagramVisualizer.Components.Helper_Class
{
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

        public ThemeSettings(string theme = "light")
        {
            if (theme == "light")
            {
                CssUrl = "https://cdn.syncfusion.com/blazor/29.1.33/styles/material.css";
                DiagramBackgroundColor = "transparent";
                GridlinesColor = "#EBE8E8";
                HighlightColor = "#e7f0e6";
                NodeFillColor = "rgb(255, 255, 255)";
                NodeStrokeColor = "rgb(188, 190, 192)";
                TextKeyColor = "blue";
                TextValueColor = "rgb(83, 83, 83)";
                TextValueNullColor = "rgb(41, 41, 41)";
                ExpandIconFillColor = "#e0dede";
                ExpandIconColor = "black";
                ExpandIconBorder = "rgb(188, 190, 192)";
                ConnectorStrokeColor = "rgb(188, 190, 192)";
                ChildCountColor = "rgb(41, 41, 41)";
                BooleanColor = "rgb(74, 145, 67)";
                NumericColor = "rgb(182, 60, 30)";
            }
            else
            {
                CssUrl = "https://cdn.syncfusion.com/blazor/29.1.33/styles/material-dark.css";
                DiagramBackgroundColor = "#1e1e1e";
                GridlinesColor = "rgb(40, 40, 40)";
                HighlightColor = "rgb(32, 97, 51, 0.5)";
                NodeFillColor = "rgb(41, 41, 41)";
                NodeStrokeColor = "rgb(66, 66, 66)";
                TextKeyColor = "rgb(107, 176, 246)";
                TextValueColor = "rgb(207, 227, 225)";
                TextValueNullColor = "rgb(151, 150, 149)";
                ExpandIconFillColor = "#1e1e1e";
                ExpandIconBorder = "rgb(66, 66, 66)";
                ExpandIconColor = "white";
                ConnectorStrokeColor = "rgb(66, 66, 66)";
                ChildCountColor = "rgb(255, 255, 255)";
                BooleanColor = "rgb(61, 226, 49)";
                NumericColor = "rgb(232, 196, 121)";
            }
        }
    }

}
