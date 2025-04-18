using Godot;
using Godot.Bridge;

namespace GDExtensionSummator;

public partial class Summator : RefCounted
{
    private int _count;

    public void Add(int value = 1)
    {
        _count += value;
    }

    public void Reset()
    {
        _count = 0;
    }

    public int GetTotal()
    {
        return _count;
    }

    internal static void BindMethods(ClassRegistrationContext context)
    {
        context.BindConstructor(() => new Summator());

        context.BindMethod(new StringName(nameof(Add)),
            new ParameterInfo(new StringName("value"), VariantType.Int, VariantTypeMetadata.Int32, 1),
            static (Summator instance, int value) =>
            {
                instance.Add(value);
            });

        context.BindMethod(new StringName(nameof(Reset)),
            static (Summator instance) =>
            {
                instance.Reset();
            });

        context.BindMethod(new StringName(nameof(GetTotal)),
            new ReturnInfo(VariantType.Int, VariantTypeMetadata.Int32),
            static (Summator instance) =>
            {
                return instance.GetTotal();
            });
    }
}
