// Runs the EditMode tests without Unity.
//
// TheVeil.Sim is compiled without engine references on purpose and TheVeil.Gen only depends
// on it, so the two assemblies that hold the generator, the fog of war, the route
// solver and the fighting all run under a plain .NET host. That is most of the game's
// logic, and until now none of it was checked between one person opening the editor
// and the next.
//
// Reflection rather than a test framework, because adding a package reference would
// mean the check only runs where that package can be restored — and the whole point of
// this directory is that it runs anywhere with a dotnet SDK.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

static class Runner
{
    static int Main(string[] args)
    {
        string only = args.Length > 0 ? args[0] : null;

        int passed = 0, ignored = 0;
        var failures = new List<string>();
        var clock = Stopwatch.StartNew();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;

            var tests = new List<MethodInfo>();
            MethodInfo setUp = null;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<TestAttribute>() != null) tests.Add(method);
                if (method.GetCustomAttribute<SetUpAttribute>() != null) setUp = method;
            }

            if (tests.Count == 0) continue;

            foreach (var test in tests)
            {
                string name = $"{type.Name}.{test.Name}";
                if (only != null && !name.Contains(only, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    // A fresh instance per test, as NUnit does, so one test cannot leave
                    // state behind for the next and turn a real failure into a mystery.
                    var fixture = Activator.CreateInstance(type);
                    setUp?.Invoke(fixture, null);
                    test.Invoke(fixture, null);
                    passed++;
                }
                catch (TargetInvocationException wrapped) when (wrapped.InnerException is IgnoreException skip)
                {
                    ignored++;
                    Console.WriteLine($"  ignored  {name}: {skip.Message}");
                }
                catch (TargetInvocationException wrapped)
                {
                    var cause = wrapped.InnerException;
                    failures.Add(cause is AssertionException
                        ? $"{name}\n  {cause.Message}"
                        : $"{name}\n  {cause?.GetType().Name}: {cause?.Message}\n{cause?.StackTrace}");
                }
            }
        }

        foreach (var failure in failures) Console.WriteLine($"FAILED  {failure}\n");

        Console.WriteLine($"{passed} passed, {failures.Count} failed, {ignored} ignored "
                          + $"in {clock.Elapsed.TotalSeconds:0.0}s");

        return failures.Count == 0 ? 0 : 1;
    }
}
