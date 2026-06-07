using System.ComponentModel;
using System.Reflection;

namespace Alife.Function.Interpreter;

public class XmlHandler
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Explain { get; set; }
    public List<XmlFunction> Functions { get; init; } = new();
    public object? Instance { get; init; }
    public bool IsImplicit { get; set; }

    public XmlHandler() {}

    public XmlHandler(object instance, string? explain = null, bool isImplicit = false)
    {
        Instance = instance;
        Type handlerType = instance.GetType();
        Name = handlerType.Name;
        Description = handlerType.GetCustomAttribute<DescriptionAttribute>()?.Description;
        IsImplicit = isImplicit;
        Explain = explain;

        foreach (MethodInfo method in handlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            XmlFunction? function = ParseFunction(method, instance);
            if (function != null)
                Functions.Add(function);
        }
    }

    static XmlFunction? ParseFunction(MethodInfo method, object handler)
    {
        XmlFunctionAttribute? functionAttribute = method.GetCustomAttribute<XmlFunctionAttribute>();
        if (functionAttribute == null)
            return null;

        ParameterInfo[] rawParameters = method.GetParameters();
        //统计参数信息
        string? contentName = functionAttribute.Mode == FunctionMode.Content ? "Content" : null;
        string? contentDescription = null;
        List<XmlParameter> normalParameters = new();
        foreach (ParameterInfo parameterInfo in rawParameters)
        {
            if (parameterInfo.Name == null)
                continue;

            string parameterName = parameterInfo.Name.ToLower();
            string? parameterDescription = parameterInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;

            //特殊参数
            if (parameterInfo.ParameterType.IsAssignableTo(typeof(XmlContext)) ||
                parameterInfo.ParameterType == typeof(CancellationToken))
                continue;

            //内容参数
            if (parameterInfo.GetCustomAttribute<XmlContentAttribute>() != null)
            {
                contentName = parameterName;
                contentDescription = parameterDescription;
                continue;
            }

            //普通参数
            if (TypeDescriptor.GetConverter(parameterInfo.ParameterType).CanConvertFrom(typeof(string)))
            {
                bool isCanNull = Nullable.GetUnderlyingType(parameterInfo.ParameterType) != null;
                Type parameterType = isCanNull ? parameterInfo.ParameterType.GenericTypeArguments[0] : parameterInfo.ParameterType;
                string parameterTypeName = parameterType.IsEnum ? string.Join(" | ", parameterType.GetEnumNames()) : parameterType.Name;
                if (isCanNull)
                    parameterTypeName += "[可选]";

                normalParameters.Add(new XmlParameter() {
                    Name = parameterName,
                    Description = parameterDescription,
                    Type = parameterTypeName,
                });
                continue;
            }

            throw new NotSupportedException("不支持的参数类型！");
        }

        //函数参数缓冲区
        object?[] parameterValuesBuffer = new object?[rawParameters.Length];

        Task Invoker(XmlContext context, CancellationToken cancellationToken)
        {
            //验证调用模式
            if (context.CallMode == CallMode.Opening || context.CallMode == CallMode.Content || context.CallMode == CallMode.Closing)
            {
                if ((functionAttribute.Mode & FunctionMode.Content) == 0)
                    throw new Exception($"调用 {method.Name} 标签的方式错误，应该使用单个自闭合标签调用。");
            }
            else if (context.CallMode == CallMode.OneShot)
            {
                if ((functionAttribute.Mode & FunctionMode.OneShot) == 0)
                    throw new Exception($"调用 {method.Name} 标签的方式错误，应该使用两个开闭标签包裹调用。");
            }

            //填充函数参数
            for (int index = 0; index < rawParameters.Length; index++)
            {
                ParameterInfo parameterInfo = rawParameters[index];

                object? result = null;
                bool isFilled = false;

                //尝试特殊值
                {
                    if (parameterInfo.ParameterType.IsInstanceOfType(context))
                    {
                        result = context;
                        isFilled = true;
                    }
                    else if (parameterInfo.ParameterType == typeof(CancellationToken))
                    {
                        result = cancellationToken;
                        isFilled = true;
                    }
                    else if (parameterInfo.GetCustomAttribute<XmlContentAttribute>() != null)
                    {
                        result = context.Content;
                        isFilled = true;
                    }
                }

                //尝试输入值
                if (isFilled == false)
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(parameterInfo.ParameterType);
                    if (converter.CanConvertFrom(typeof(string)))
                    {
                        //可以由字符串转换
                        if (parameterInfo.Name != null && context.Parameters.TryGetValue(parameterInfo.Name.ToLower(), out string? value))
                        {
                            //有传入的字符串参数
                            try
                            {
                                result = converter.ConvertFromInvariantString(value);
                                isFilled = true;
                            }
                            catch (Exception)
                            {
                                // 解析失败
                            }
                        }
                    }
                }


                //尝试默认值
                if (isFilled == false)
                {
                    object? defaultValue = rawParameters[index].DefaultValue;
                    if (defaultValue != DBNull.Value)
                    {
                        result = defaultValue;
                        isFilled = true;
                    }
                }

                if (isFilled)
                    parameterValuesBuffer[index] = result;
                else
                    throw new Exception($"{method.Name} 标签缺少 {parameterInfo.Name} 参数，或参数值解析失败！");
            }

            //调用
            object? back = method.Invoke(handler, parameterValuesBuffer);

            //处理返回值
            if (back is Task task) return task;
            return Task.CompletedTask;
        }

        return new XmlFunction {
            Name = functionAttribute.Name ?? method.Name.ToLower(),
            Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description,
            ContentName = contentName,
            ContentDescription = contentDescription,
            Parameters = normalParameters,
            Order = functionAttribute.Order,
            Invoker = Invoker,
        };
    }
}

[Flags]
public enum FunctionMode
{
    All = ~0,
    Content = 0b_01,
    OneShot = 0b_10,
}

[AttributeUsage(AttributeTargets.Method)]
public class XmlFunctionAttribute(FunctionMode mode, string? name = null, int order = 0) : Attribute
{
    public string? Name { get; } = name;
    public int Order { get; } = order;
    public FunctionMode Mode { get; } = mode;
}

public class XmlFunction : IComparable<XmlFunction>
{
    public required string Name { get; init; }
    public int Order { get; init; }
    public FunctionMode Mode { get; init; }
    public string? Description { get; init; }
    public string? ContentName { get; init; }
    public string? ContentDescription { get; init; }
    public List<XmlParameter> Parameters { get; init; } = new();
    public required Func<XmlContext, CancellationToken, Task> Invoker { get; init; }

    public int CompareTo(XmlFunction? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return Order.CompareTo(other.Order);
    }
}

public enum CallMode
{
    Opening = 0,
    Content = 1,
    Closing = 2,
    OneShot = 3,
}

public class XmlContext
{
    public CallMode CallMode { get; init; }
    public string Content { get; set; } = "";
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class XmlContentAttribute : Attribute {}

public record XmlParameter
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
}
