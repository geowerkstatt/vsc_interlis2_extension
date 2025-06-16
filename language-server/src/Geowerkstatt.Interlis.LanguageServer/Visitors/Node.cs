using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors
{
    internal class Node
    {
        public string? Id { get; set; }
        public NodeData? Data { get; set; }
        public string? Label { get; set; }
        public NodePosition? Position { get; set; }
        public NodeStyle? Style { get; set; }

    }

    internal class NodeData
    {
        public string? Title { get; set; }
        public List<string> Attributes { get; set; } = new List<string>();
    }

    internal class NodePosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    internal class NodeStyle {
        public string Background { get; set; } = "#ffffff"; // Default white
        public string Color { get; set; } = "#000000"; // Default black
        public string Border { get; set; } = "2px solid black";  // Default black
    }
}
