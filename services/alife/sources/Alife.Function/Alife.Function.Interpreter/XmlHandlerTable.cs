using System.Text;

namespace Alife.Function.Interpreter;

public class XmlHandlerTable
{
    public IReadOnlyList<XmlHandler> Handlers => xmlHandlers;

    public void Register(XmlHandler handler)
    {
        xmlHandlers.Add(handler);
        foreach (XmlFunction xmlFunction in handler.Functions)
        {
            if (xmlFunctions.TryGetValue(xmlFunction.Name, out SortedSet<XmlFunction>? xmlFunctionGroup) == false)
            {
                xmlFunctionGroup = new SortedSet<XmlFunction>();
                xmlFunctions[xmlFunction.Name] = xmlFunctionGroup;
            }

            xmlFunctionGroup.Add(xmlFunction);
        }
    }

    public void Unregister(XmlHandler handler)
    {
        xmlHandlers.Remove(handler);
        foreach (XmlFunction xmlHandlerFunction in handler.Functions)
        {
            if (xmlFunctions.TryGetValue(xmlHandlerFunction.Name, out SortedSet<XmlFunction>? xmlFunctionGroup))
                xmlFunctionGroup.Remove(xmlHandlerFunction);
        }
    }

    public string Document()
    {
        StringBuilder sb = new();
        foreach (XmlHandler handler in xmlHandlers)
        {
            if (handler.IsImplicit)
                continue;
            sb.AppendLine(Document(handler));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public string Document(XmlHandler handler)
    {
        StringBuilder sb = new();
        sb.AppendLine($"> 来源：{handler.Name}");
        if (string.IsNullOrEmpty(handler.Description) == false)
            sb.AppendLine($"服务描述：{handler.Description}");

        sb.AppendLine("提供的标签：");
        foreach (XmlFunction function in handler.Functions)
        {
            sb.Append($"- <{function.Name}");
            foreach (XmlParameter param in function.Parameters)
            {
                string pDesc = string.IsNullOrEmpty(param.Description) ? "" : $"（{param.Description}）";
                sb.Append($" {param.Name}=\"{param.Type}\"{pDesc}");
            }

            if (function.ContentName != null)
            {
                sb.Append(">");
                string cDesc = string.IsNullOrEmpty(function.ContentDescription)
                    ? ""
                    : $"（{function.ContentDescription}）";
                sb.Append($"{function.ContentName}{cDesc}</{function.Name}>");
            }
            else
            {
                sb.Append(" />");
            }

            if (string.IsNullOrEmpty(function.Description) == false)
                sb.Append($" : {function.Description}");

            sb.AppendLine();
        }

        if (string.IsNullOrEmpty(handler.Explain) == false)
        {
            sb.AppendLine("附加说明：");
            sb.AppendLine("---");
            sb.AppendLine($"{handler.Explain}");
            sb.AppendLine("---");
        }

        return sb.ToString().TrimEnd();
    }

    public string DocumentOnlyFunction()
    {
        StringBuilder sb = new();
        foreach (XmlFunction function in xmlFunctions.Values.SelectMany(set => set))
        {
            sb.Append($"- <{function.Name}");
            foreach (XmlParameter param in function.Parameters)
            {
                string pDesc = string.IsNullOrEmpty(param.Description) ? "" : $"（{param.Description}）";
                sb.Append($" {param.Name}=\"{param.Type}\"{pDesc}");
            }

            if (function.ContentName != null)
            {
                sb.Append(">");
                string cDesc = string.IsNullOrEmpty(function.ContentDescription)
                    ? ""
                    : $"（{function.ContentDescription}）";
                sb.Append($"{function.ContentName}{cDesc}</{function.Name}>");
            }
            else
            {
                sb.Append(" />");
            }

            if (string.IsNullOrEmpty(function.Description) == false)
                sb.Append($"：{function.Description}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task Handle(string name, XmlContext tagContext, CancellationToken cancellationToken = default)
    {
        SortedSet<XmlFunction>? xmlFunctionGroup = xmlFunctions.GetValueOrDefault(name.ToLower());
        if (xmlFunctionGroup == null || xmlFunctionGroup.Count == 0)
            throw new Exception($"未找到名为 {name} 的可调用函数");
        foreach (XmlFunction xmlFunction in xmlFunctionGroup)
            await xmlFunction.Invoker(tagContext, cancellationToken);
    }

    readonly List<XmlHandler> xmlHandlers = new();
    readonly Dictionary<string, SortedSet<XmlFunction>> xmlFunctions = new();
}