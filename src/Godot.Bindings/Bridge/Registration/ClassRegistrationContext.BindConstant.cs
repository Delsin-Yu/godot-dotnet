using System;
using System.Collections.Generic;
using Godot.NativeInterop;

namespace Godot.Bridge;

partial class ClassRegistrationContext
{
    private readonly HashSet<StringName> _registeredConstants = [];

    /// <summary>
    /// Register a constant in the class.
    /// </summary>
    /// <param name="constantInfo">Information that describes the constant to register.</param>
    /// <exception cref="ArgumentException">
    /// A constant has already been registered with the same name.
    /// </exception>
    public unsafe void BindConstant(ConstantInfo constantInfo)
    {
        if (!_registeredConstants.Add(constantInfo.Name))
        {
            throw new ArgumentException(SR.FormatArgument_ConstantAlreadyRegistered(constantInfo.Name, ClassName), nameof(constantInfo));
        }

        StringName enumName = constantInfo.EnumName ?? StringName.Empty;

        if (enumName.IsEmpty && constantInfo.IsFlagsEnum)
        {
            throw new ArgumentException(SR.FormatArgument_ConstantWithoutEnumCantBeFlag(constantInfo.Name), nameof(constantInfo));
        }

        _registerBindingActions.Enqueue(() =>
        {
            NativeGodotStringName constantNameNative = constantInfo.Name.NativeValue.DangerousSelfRef;
            NativeGodotStringName enumNameNative = enumName.NativeValue.DangerousSelfRef;

            NativeGodotStringName classNameNative = ClassName.NativeValue.DangerousSelfRef;

            GodotBridge.GDExtensionInterface.classdb_register_extension_class_integer_constant(GodotBridge.LibraryPtr, &classNameNative, &enumNameNative, &constantNameNative, constantInfo.Value, constantInfo.IsFlagsEnum);
        });
    }
}
