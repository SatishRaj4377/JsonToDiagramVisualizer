using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Syncfusion.Blazor.Diagram;

public static class XmlDiagramParser
{
    private const double DEFAULT_NODE_WIDTH = 150;
    private const double DEFAULT_NODE_HEIGHT = 50;

    public static DiagramData ProcessData(string xmlString)
    {
        var data = new DiagramData();
        XDocument doc;
        try
        {
            doc = XDocument.Parse($"<__root__>{xmlString}</__root__>");
        }
        catch
        {
            return data;
        }
        var allEls = doc.Root.Elements().ToList();
        if (!allEls.Any())
            return data;

        var topGroups = GroupByTag(allEls);
        var primitives = new List<XElement>();
        var complexes = new List<XElement>();
        var arrays = new List<(string tag, List<XElement> items)>();

        foreach (var kv in topGroups)
        {
            if (kv.Value.Count > 1)
                arrays.Add((kv.Key, kv.Value));
            else if (!kv.Value[0].Elements().Any())
                primitives.Add(kv.Value[0]);
            else
                complexes.Add(kv.Value[0]);
        }

        string mergedRootId = null;
        if (primitives.Any())
        {
            mergedRootId = "RootMerged";
            var ann = new DiagramObjectCollection<ShapeAnnotation>();
            var contentLines = new List<string>();
            foreach (var el in primitives)
            {
                var key = el.Name.LocalName;
                var val = el.Value.Trim();
                ann.Add(new ShapeAnnotation { ID = $"Key_{key}", Content = $"{key}:" });
                ann.Add(new ShapeAnnotation { ID = $"Value_{key}", Content = FormatValue(val) });
                contentLines.Add($"{key}: {FormatValue(val)}");
            }
            data.Nodes.Add(new Node
            {
                ID = mergedRootId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = ann,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = true,
                    ["mergedContent"] = string.Join("\n", contentLines)
                },
                Data = new { path = "Root", title = string.Join("\n", contentLines), actualdata = string.Join("\n", contentLines) }
            });
        }

        for (int i = 0; i < complexes.Count; i++)
        {
            var el = complexes[i];
            var nodeId = ToPascal(el.Name.LocalName) + i;
            ProcessElement(el, nodeId, mergedRootId, el.Name.LocalName, $"Root.{el.Name.LocalName}", data);
        }

        foreach (var (tag, items) in arrays)
            EmitArrayBlock(tag, items, mergedRootId, "Root", data);

        InjectMainRoot(data);
        return data;
    }

    private static void ProcessElement(
        XElement el,
        string nodeId,
        string parentId,
        string keyName,
        string path,
        DiagramData data)
    {
        var children = el.Elements().ToList();
        if (!children.Any())
        {
            var content = $"{keyName}: {FormatValue(el.Value.Trim())}";
            var ann = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID = $"Key_{keyName}", Content = content }
            };
            data.Nodes.Add(new Node
            {
                ID = nodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = ann,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = true,
                    ["mergedContent"] = content
                },
                Data = new { path, title = content, actualdata = content }
            });
            if (parentId != null)
                data.Connectors.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });
            return;
        }

        var groups = GroupByTag(children);
        var leafEls = new List<XElement>();
        var complexEls = new List<XElement>();
        var arrayEls = new List<(string tag, List<XElement> items)>();
        foreach (var kv in groups)
        {
            if (kv.Value.Count > 1)
                arrayEls.Add((kv.Key, kv.Value));
            else if (!kv.Value[0].Elements().Any())
                leafEls.Add(kv.Value[0]);
            else
                complexEls.Add(kv.Value[0]);
        }

        int displayCount = complexEls.Count + arrayEls.Count + (leafEls.Any() ? 1 : 0);
        var folderAnn = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { Content = keyName } };
        if (displayCount > 0)
            folderAnn.Add(new ShapeAnnotation { Content = $"{{{displayCount}}}" });
        data.Nodes.Add(new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = folderAnn,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = $"{keyName}  {{{displayCount}}}"
            },
            Data = new { path, title = keyName, actualdata = keyName }
        });
        if (parentId != null)
            data.Connectors.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });

        if (leafEls.Any())
        {
            var leafId = $"{nodeId}-leaf";
            var la = new DiagramObjectCollection<ShapeAnnotation>();
            var lc = new List<string>();
            foreach (var ch in leafEls)
            {
                var k = ch.Name.LocalName;
                var v = ch.Value.Trim();
                la.Add(new ShapeAnnotation { ID = $"Key_{k}", Content = $"{k}:" });
                la.Add(new ShapeAnnotation { ID = $"Value_{k}", Content = FormatValue(v) });
                lc.Add($"{k}: {FormatValue(v)}");
            }
            var merged = string.Join("\n", lc);
            data.Nodes.Add(new Node
            {
                ID = leafId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = la,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = true,
                    ["mergedContent"] = merged
                },
                Data = new { path = $"{path}.leaf", title = merged, actualdata = merged }
            });
            data.Connectors.Add(new Connector { ID = $"connector-{nodeId}-{leafId}", SourceID = nodeId, TargetID = leafId });
        }

        foreach (var (tag, items) in arrayEls)
            EmitArrayBlock(tag, items, nodeId, path, data);

        for (int i = 0; i < complexEls.Count; i++)
        {
            var ch = complexEls[i];
            var cid = $"{nodeId}-{ToPascal(ch.Name.LocalName)}{i}";
            ProcessElement(ch, cid, nodeId, ch.Name.LocalName, $"{path}.{ch.Name.LocalName}", data);
        }
    }

    private static void EmitArrayBlock(string tag, List<XElement> items, string parentId, string parentPath, DiagramData data)
    {
        var parentNodeId = parentId != null ? $"{parentId}-{ToPascal(tag)}" : ToPascal(tag);
        // 1) Folder for the array
        data.Nodes.Add(new Node
        {
            ID = parentNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = tag },
                new ShapeAnnotation { Content = $"{{{items.Count}}}" }
            },
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = $"{tag}  {{{items.Count}}}"
            },
            Data = new { path = $"{parentPath}.{tag}", title = tag, actualdata = tag }
        });
        if (parentId != null)
            data.Connectors.Add(new Connector { ID = $"connector-{parentId}-{parentNodeId}", SourceID = parentId, TargetID = parentNodeId });

        // a) Primitive array
        if (items.All(it => !it.Elements().Any()))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var val = items[i].Value.Trim();
                var leafId = $"{parentNodeId}-{i}";
                data.Nodes.Add(new Node
                {
                    ID = leafId,
                    Width = DEFAULT_NODE_WIDTH,
                    Height = DEFAULT_NODE_HEIGHT,
                    Annotations = new DiagramObjectCollection<ShapeAnnotation>
                    {
                        new ShapeAnnotation { ID = $"Value_{tag}", Content = FormatValue(val) }
                    },
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["isLeaf"] = true,
                        ["mergedContent"] = FormatValue(val)
                    },
                    Data = new { path = $"{parentPath}.{tag}[{i}]", title = FormatValue(val), actualdata = FormatValue(val) }
                });
                data.Connectors.Add(new Connector { ID = $"connector-{parentNodeId}-{leafId}", SourceID = parentNodeId, TargetID = leafId });
            }
            return;
        }

        // b) Object array (all child tags unique and leaf)
        bool isObjectArray = items.All(it =>
        {
            var grp = GroupByTag(it.Elements());
            return grp.Values.All(arr => arr.Count == 1 && !arr[0].Elements().Any());
        });
        if (isObjectArray)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var obj = items[i];
                var leafId = $"{parentNodeId}-{i}";
                var la = new DiagramObjectCollection<ShapeAnnotation>();
                var lc = new List<string>();
                foreach (var ch in obj.Elements())
                {
                    var k = ch.Name.LocalName;
                    var v = ch.Value.Trim();
                    la.Add(new ShapeAnnotation { ID = $"Key_{k}", Content = $"{k}:" });
                    la.Add(new ShapeAnnotation { ID = $"Value_{k}", Content = FormatValue(v) });
                    lc.Add($"{k}: {FormatValue(v)}");
                }
                var merged = string.Join("\n", lc);
                data.Nodes.Add(new Node
                {
                    ID = leafId,
                    Width = DEFAULT_NODE_WIDTH,
                    Height = DEFAULT_NODE_HEIGHT,
                    Annotations = la,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["isLeaf"] = true,
                        ["mergedContent"] = merged
                    },
                    Data = new { path = $"{parentPath}.{tag}[{i}]", title = merged, actualdata = merged }
                });
                data.Connectors.Add(new Connector { ID = $"connector-{parentNodeId}-{leafId}", SourceID = parentNodeId, TargetID = leafId });
            }
            return;
        }

        // c) Mixed items: primitives + nested arrays/objects
        bool isMixed = items.All(it =>
        {
            var grp = GroupByTag(it.Elements());
            var hasPrim = grp.Any(kv => kv.Value.Count == 1 && !kv.Value[0].Elements().Any());
            var hasComplex = grp.Any(kv => kv.Value.Count > 1 || kv.Value[0].Elements().Any());
            return hasPrim && hasComplex;
        });
        if (isMixed)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var grp = GroupByTag(it.Elements());
                var primKeys = grp.Where(kv => kv.Value.Count == 1 && !kv.Value[0].Elements().Any())
                                    .Select(kv => kv.Key)
                                    .ToList();
                var leafId = $"{parentNodeId}-{i}";
                var la = new DiagramObjectCollection<ShapeAnnotation>();
                var lc = new List<string>();
                foreach (var k in primKeys)
                {
                    var v = grp[k][0].Value.Trim();
                    la.Add(new ShapeAnnotation { ID = $"Key_{k}", Content = $"{k}:" });
                    la.Add(new ShapeAnnotation { ID = $"Value_{k}", Content = FormatValue(v) });
                    lc.Add($"{k}: {FormatValue(v)}");
                }
                var merged = string.Join("\n", lc);
                data.Nodes.Add(new Node
                {
                    ID = leafId,
                    Width = DEFAULT_NODE_WIDTH,
                    Height = DEFAULT_NODE_HEIGHT,
                    Annotations = la,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["isLeaf"] = true,
                        ["mergedContent"] = merged
                    },
                    Data = new { path = $"{parentPath}.{tag}[{i}]", title = merged, actualdata = merged }
                });
                data.Connectors.Add(new Connector { ID = $"connector-{parentNodeId}-{leafId}", SourceID = parentNodeId, TargetID = leafId });

                foreach (var kv in grp.Where(kv => kv.Value.Count > 1 || kv.Value[0].Elements().Any()))
                {
                    if (kv.Value.Count > 1 && !kv.Value[0].Elements().Any())
                    {
                        EmitArrayBlock(kv.Key, kv.Value, leafId, $"{parentPath}.{tag}[{i}]", data);
                    }
                    else if (kv.Value.Count > 1)
                    {
                        EmitArrayBlock(kv.Key, kv.Value, leafId, $"{parentPath}.{tag}[{i}]", data);
                    }
                    else
                    {
                        var nested = kv.Value[0];
                        var childId = $"{leafId}-{ToPascal(kv.Key)}";
                        ProcessElement(nested, childId, leafId, kv.Key, $"{parentPath}.{tag}[{i}].{kv.Key}", data);
                    }
                }
            }
            return;
        }

        // d) Fallback full recursion
        for (int i = 0; i < items.Count; i++)
            ProcessElement(items[i], $"{parentNodeId}-{i}", parentNodeId, tag, $"{parentPath}.{tag}[{i}]", data);
    }

    private static void InjectMainRoot(DiagramData data)
    {
        var allIds = data.Nodes.Select(n => n.ID).ToList();
        var hasIncoming = new HashSet<string>(data.Connectors.Select(c => c.TargetID));
        var roots = allIds.Where(id => !hasIncoming.Contains(id)).ToList();
        if (roots.Count > 1)
        {
            const string mainRootId = "main-root";
            data.Nodes.Add(new Node
            {
                ID = mainRootId,
                Width = 40,
                Height = 40,
                Annotations = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { Content = "" } },
                Data = new { path = "MainRoot", title = "", actualdata = "" }
            });
            foreach (var r in roots)
                data.Connectors.Add(new Connector { ID = $"connector-{mainRootId}-{r}", SourceID = mainRootId, TargetID = r });
        }
    }

    private static Dictionary<string, List<XElement>> GroupByTag(IEnumerable<XElement> elems)
        => elems.GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.ToList());

    private static string ToPascal(string s)
        => string.Concat(s.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpper(p[0]) + p.Substring(1)));

    private static string FormatValue(string v)
    {
        if (bool.TryParse(v, out var b)) return b.ToString().ToLower();
        if (double.TryParse(v, out var d)) return d.ToString();
        return $"\"{v}\"";
    }
}

