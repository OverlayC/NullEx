using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace NullEx
{
    public class EntryPoint
    {
        public string Namespace { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public int Score { get; set; }

        public override string ToString()
        {
            string ns = string.IsNullOrEmpty(Namespace) ? "<global>" : Namespace;
            return $"{ns}.{ClassName}.{MethodName}()";
        }
    }

    public static class AssemblyScanner
    {
        private static readonly string[] PreferredMethodNames =
        {
            "Main", "Load", "Loader", "Init", "Initialize", "Start",
            "OnLoad", "Entry", "EntryPoint", "Inject", "Run", "Execute",
            "Bootstrap", "Awake", "Setup"
        };

        private static readonly string[] PreferredClassNames =
        {
            "Loader", "Main", "Entry", "Bootstrap", "Injector", "Mod",
            "Plugin", "Initializer", "Cheat", "Hack", "Menu"
        };

        public static List<EntryPoint> Scan(string dllPath)
        {
            var results = new List<EntryPoint>();

            using (var module = ModuleDefinition.ReadModule(dllPath))
            {
                foreach (var type in module.Types)
                {
                    CollectFromType(type, results);
                }
            }

            return results
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Namespace)
                .ThenBy(e => e.ClassName)
                .ThenBy(e => e.MethodName)
                .ToList();
        }

        private static void CollectFromType(TypeDefinition type, List<EntryPoint> results)
        {
            if (!type.IsPublic && !type.IsNestedPublic) return;

            if (type.Name.StartsWith("<") || type.Name.Contains("__")) return;

            foreach (var method in type.Methods)
            {
                if (!method.IsPublic) continue;
                if (!method.IsStatic) continue;
                if (method.ReturnType.FullName != "System.Void") continue;
                if (method.Parameters.Count != 0) continue;

                if (method.Name.StartsWith("<") || method.Name.Contains("__")) continue;

                int score = 0;

                foreach (var preferred in PreferredClassNames)
                {
                    if (type.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                        score += 50;
                    else if (type.Name.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 20;
                }

                foreach (var preferred in PreferredMethodNames)
                {
                    if (method.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                        score += 40;
                    else if (method.Name.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 15;
                }

                if (!string.IsNullOrEmpty(type.Namespace))
                    score += 5;

                results.Add(new EntryPoint
                {
                    Namespace = type.Namespace ?? string.Empty,
                    ClassName = type.Name,
                    MethodName = method.Name,
                    Score = score
                });
            }

            foreach (var nested in type.NestedTypes)
                CollectFromType(nested, results);
        }
    }
}