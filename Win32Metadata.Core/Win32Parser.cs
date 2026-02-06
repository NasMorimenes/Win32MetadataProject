using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using Win32Metadata.Core.Models;

namespace Win32Metadata.Core;

public class Win32Parser : IDisposable
{
    private readonly ModuleDefMD _module;
    public Dictionary<string, TypeDefinition> TypeCache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Win32Parser(string winmdPath)
    {
        _module = ModuleDefMD.Load(winmdPath);
    }

    public Win32Function? ExtractFunction(MethodDef method)
    {
        if (method.ImplMap == null || method.ImplMap.Module == null) return null;

        var parameters = new List<Win32Parameter>();
        var methodSig = method.MethodSig;
        if (methodSig == null) return null;

        for (int i = 0; i < methodSig.Params.Count; i++)
        {
            var pSig = methodSig.Params[i];
            var pDef = method.ParamDefs.FirstOrDefault(pd => pd.Sequence == i + 1);
            
            RecordComplexType(pSig);

            parameters.Add(new Win32Parameter(
                pDef?.Name.ToString() ?? $"p{i}",
                pSig.TypeName,
                pSig.IsPointer || pSig.IsByRef,
                pDef?.IsOptional ?? false
            ));
        }

        RecordComplexType(method.ReturnType);

        return new Win32Function(
            method.Name.ToString(),
            method.ImplMap.Module.Name.ToString().ToLower().Replace(".dll", ""),
            method.ReturnType.TypeName,
            parameters
        );
    }

    public IEnumerable<MethodDef> EnumerateAllMethods() 
        => _module.GetTypes().SelectMany(t => t.Methods);

    private void RecordComplexType(TypeSig sig)
    {
        if (sig == null) return;
        
        TypeSig baseSig = sig;
        while (baseSig.Next != null) baseSig = baseSig.Next;

        string typeName = baseSig.TypeName;
        if (baseSig.IsPrimitive || TypeCache.ContainsKey(typeName)) return;

        var typeDef = baseSig.TryGetTypeDef();
        if (typeDef == null) return;

        if (typeDef.IsEnum)
        {
            var values = typeDef.Fields.Where(f => f.IsLiteral)
                               .ToDictionary(f => f.Name.ToString(), f => f.Constant?.Value ?? 0);
            TypeCache[typeName] = new TypeDefinition("Enum", values);
        }
        else if (typeDef.IsValueType)
        {
            string kind = typeDef.IsExplicitLayout ? "Union" : "Struct";
            var fields = typeDef.Fields.Where(f => !f.IsStatic)
                                .ToDictionary(f => f.Name.ToString(), f => f.FieldSig.Type.TypeName);
            TypeCache[typeName] = new TypeDefinition(kind, fields);
        }
    }

    public void Dispose() => _module.Dispose();
}