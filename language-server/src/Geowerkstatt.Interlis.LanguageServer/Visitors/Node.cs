using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors
{
    public class ReactflowResponse
    {
        public List<Node>? Nodes { get; set; }
        public List<Edge>? Edges { get; set; }

    }

    public class Edge
    {
        public string? Id { get; set; }
        public string? Source { get; set; }
        public string? Target { get; set; }
    }

    public class Node
    {
        public string? Id { get; set; }
        public NodeData? Data { get; set; }
        public NodeStyle? Style { get; set; }
        public NodePosition? Position { get; set; }

    }

    public class NodeData
    {
        public string? Title { get; set; }
        public List<string> Attributes { get; set; } = new List<string>();
    }

    public class NodeStyle
    {
        public string Background { get; set; } = "#ffffff"; // Default white
        public string Color { get; set; } = "#000000"; // Default black
        public string Border { get; set; } = "2px solid black";  // Default black
    }

    public class NodePosition
    {
        [JsonPropertyName("x")]
        public int X { get; set; } = 0;

        [JsonPropertyName("y")]
        public int Y { get; set; } = 0;
    }
}
