// NUnit, near enough to run the EditMode tests outside Unity.
//
// It started as a stand-in that asserted nothing, so the tests compiled here and were
// only ever *run* by somebody opening the editor. That is half a safety net: it caught
// a test that would not build and missed every test that would fail. The assertions
// below are real, and Runner.cs executes them.
//
// Only the forms the suite actually uses are implemented. An unimplemented one should
// fail loudly rather than quietly pass, which is why there is no catch-all overload.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public class IgnoreException : Exception
    {
        public IgnoreException(string message) : base(message) { }
    }

    /// <summary>A predicate over the actual value, plus enough words to explain itself.</summary>
    public class Constraint
    {
        internal Func<object, bool> Holds;
        internal string Wanted;

        internal Constraint(Func<object, bool> holds, string wanted)
        {
            Holds = holds;
            Wanted = wanted;
        }
    }

    /// <summary>Equality, which is the only constraint a tolerance can be attached to.</summary>
    public sealed class EqualityConstraint : Constraint
    {
        internal object Expected;
        internal double Tolerance;

        internal EqualityConstraint(object expected) : base(null, null)
        {
            Expected = expected;
            Rebuild();
        }

        internal void Rebuild()
        {
            var expected = Expected;
            double tolerance = Tolerance;

            Holds = actual => Numbers.Equal(actual, expected, tolerance);
            Wanted = tolerance > 0d
                ? $"{Numbers.Show(expected)} ± {Numbers.Show(tolerance)}"
                : Numbers.Show(expected);
        }
    }

    /// <summary>
    /// Comparison and conversion in one place.
    ///
    /// Everything arrives boxed as object, and a test comparing an int to a float is
    /// ordinary — so numbers are compared as doubles when both sides are numbers, and
    /// by Equals otherwise. Getting this wrong would make the whole suite pass.
    /// </summary>
    internal static class Numbers
    {
        internal static bool IsNumber(object value)
            => value is sbyte || value is byte || value is short || value is ushort
            || value is int || value is uint || value is long || value is ulong
            || value is float || value is double || value is decimal;

        internal static double ToDouble(object value)
            => Convert.ToDouble(value, CultureInfo.InvariantCulture);

        internal static bool Equal(object actual, object expected, double tolerance)
        {
            if (IsNumber(actual) && IsNumber(expected))
            {
                double difference = Math.Abs(ToDouble(actual) - ToDouble(expected));

                // A zero tolerance still has to survive the float-to-double widening
                // that boxing a float and asking for a double performs, or every exact
                // comparison between two floats fails on the seventh decimal.
                if (tolerance <= 0d) tolerance = 1e-6d * Math.Max(1d, Math.Abs(ToDouble(expected)));
                return difference <= tolerance;
            }

            if (actual == null || expected == null) return ReferenceEquals(actual, expected);
            return actual.Equals(expected);
        }

        internal static int Compare(object a, object b)
        {
            if (IsNumber(a) && IsNumber(b)) return ToDouble(a).CompareTo(ToDouble(b));
            return Comparer<object>.Default.Compare(a, b);
        }

        internal static string Show(object value)
        {
            if (value == null) return "null";
            if (IsNumber(value)) return ToDouble(value).ToString("0.######", CultureInfo.InvariantCulture);
            return value.ToString();
        }
    }

    public static class Is
    {
        public static Constraint EqualTo(object v) => new EqualityConstraint(v);

        public static Constraint InRange(object low, object high)
            => new Constraint(a => Numbers.Compare(a, low) >= 0 && Numbers.Compare(a, high) <= 0,
                              $"between {Numbers.Show(low)} and {Numbers.Show(high)}");

        public static Constraint GreaterThan(object v)
            => new Constraint(a => Numbers.Compare(a, v) > 0, $"greater than {Numbers.Show(v)}");

        public static Constraint GreaterThanOrEqualTo(object v)
            => new Constraint(a => Numbers.Compare(a, v) >= 0, $"at least {Numbers.Show(v)}");

        public static Constraint LessThan(object v)
            => new Constraint(a => Numbers.Compare(a, v) < 0, $"less than {Numbers.Show(v)}");

        public static Constraint LessThanOrEqualTo(object v)
            => new Constraint(a => Numbers.Compare(a, v) <= 0, $"at most {Numbers.Show(v)}");

        public static Constraint True => new Constraint(a => a is bool b && b, "true");
        public static Constraint False => new Constraint(a => a is bool b && !b, "false");
        public static Constraint Null => new Constraint(a => a == null, "null");
    }

    public static class ConstraintExtensions
    {
        public static Constraint Within(this Constraint c, object tolerance)
        {
            if (c is EqualityConstraint equality)
            {
                equality.Tolerance = Numbers.ToDouble(tolerance);
                equality.Rebuild();
                return equality;
            }

            throw new NotSupportedException("Within applies to Is.EqualTo only.");
        }

        /// <summary>Reads the tolerance as a percentage of the expected value.</summary>
        public static Constraint Percent(this Constraint c)
        {
            if (c is EqualityConstraint equality)
            {
                equality.Tolerance = Math.Abs(Numbers.ToDouble(equality.Expected)) * equality.Tolerance / 100d;
                equality.Rebuild();
                return equality;
            }

            throw new NotSupportedException("Percent applies to Is.EqualTo only.");
        }
    }

    public static class Assert
    {
        static void Fail(string what, string message)
            => throw new AssertionException(message == null ? what : $"{message}\n  {what}");

        public static void That(object actual, Constraint c, string message = null)
        {
            if (c == null) throw new AssertionException("no constraint given");
            if (!c.Holds(actual))
                Fail($"expected {c.Wanted}, was {Numbers.Show(actual)}", message);
        }

        public static void That(bool condition, string message = null)
        {
            if (!condition) Fail("expected true, was false", message);
        }

        public static void AreEqual(object a, object b, string message = null)
        {
            if (!Numbers.Equal(b, a, 0d))
                Fail($"expected {Numbers.Show(a)}, was {Numbers.Show(b)}", message);
        }

        public static void AreEqual(double a, double b, double delta, string message = null)
        {
            if (Math.Abs(a - b) > delta)
                Fail($"expected {Numbers.Show(a)} ± {Numbers.Show(delta)}, was {Numbers.Show(b)}", message);
        }

        public static void AreNotEqual(object a, object b, string message = null)
        {
            if (Numbers.Equal(b, a, 0d)) Fail($"expected something other than {Numbers.Show(a)}", message);
        }

        public static void IsTrue(bool c, string message = null)
        {
            if (!c) Fail("expected true, was false", message);
        }

        public static void IsFalse(bool c, string message = null)
        {
            if (c) Fail("expected false, was true", message);
        }

        public static void IsNull(object o, string message = null)
        {
            if (o != null) Fail($"expected null, was {o}", message);
        }

        public static void IsNotNull(object o, string message = null)
        {
            if (o == null) Fail("expected something, was null", message);
        }

        public static void Greater(IComparable a, IComparable b, string message = null)
        {
            if (Numbers.Compare(a, b) <= 0)
                Fail($"expected {Numbers.Show(a)} to be greater than {Numbers.Show(b)}", message);
        }

        public static void GreaterOrEqual(IComparable a, IComparable b, string message = null)
        {
            if (Numbers.Compare(a, b) < 0)
                Fail($"expected {Numbers.Show(a)} to be at least {Numbers.Show(b)}", message);
        }

        public static void Less(IComparable a, IComparable b, string message = null)
        {
            if (Numbers.Compare(a, b) >= 0)
                Fail($"expected {Numbers.Show(a)} to be less than {Numbers.Show(b)}", message);
        }

        public static void LessOrEqual(IComparable a, IComparable b, string message = null)
        {
            if (Numbers.Compare(a, b) > 0)
                Fail($"expected {Numbers.Show(a)} to be at most {Numbers.Show(b)}", message);
        }

        public static void Fail(string message = null)
            => throw new AssertionException(message ?? "failed");

        public static void Pass(string message = null) { }

        public static void Ignore(string message = null)
            => throw new IgnoreException(message ?? "ignored");

        public static void AreSame(object a, object b, string message = null)
        {
            if (!ReferenceEquals(a, b)) Fail("expected the same object", message);
        }

        public static void AreNotSame(object a, object b, string message = null)
        {
            if (ReferenceEquals(a, b)) Fail("expected a different object", message);
        }

        public static void IsEmpty(IEnumerable a, string message = null)
        {
            if (Count(a) != 0) Fail($"expected nothing, found {Count(a)}", message);
        }

        public static void IsNotEmpty(IEnumerable a, string message = null)
        {
            if (Count(a) == 0) Fail("expected something, found nothing", message);
        }

        public static void IsEmpty(string a, string message = null)
        {
            if (!string.IsNullOrEmpty(a)) Fail($"expected an empty string, was \"{a}\"", message);
        }

        public static void Contains(object item, ICollection collection, string message = null)
        {
            if (!Has(collection, item)) Fail($"{Numbers.Show(item)} was not in the collection", message);
        }

        public static void IsInstanceOf<T>(object o, string message = null)
        {
            if (!(o is T)) Fail($"expected a {typeof(T).Name}, was {o?.GetType().Name ?? "null"}", message);
        }

        public static void Throws<T>(Action code, string message = null)
        {
            try { code(); }
            catch (Exception e) when (e is T) { return; }
            Fail($"expected a {typeof(T).Name}", message);
        }

        public static void DoesNotThrow(Action code, string message = null)
        {
            try { code(); }
            catch (Exception e) { Fail($"threw {e.GetType().Name}: {e.Message}", message); }
        }

        internal static int Count(IEnumerable items)
        {
            if (items == null) return 0;
            int n = 0;
            foreach (var unused in items) n++;
            return n;
        }

        internal static bool Has(IEnumerable items, object item)
        {
            if (items == null) return false;
            foreach (var candidate in items)
                if (Numbers.Equal(candidate, item, 0d)) return true;
            return false;
        }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable a, IEnumerable b, string message = null)
        {
            var left = List(a);
            var right = List(b);

            if (left.Count != right.Count)
                throw new AssertionException(Say(message, $"lengths differ: {left.Count} and {right.Count}"));

            for (int i = 0; i < left.Count; i++)
                if (!Numbers.Equal(left[i], right[i], 0d))
                    throw new AssertionException(
                        Say(message, $"differ at {i}: {Numbers.Show(left[i])} and {Numbers.Show(right[i])}"));
        }

        public static void AreEquivalent(IEnumerable a, IEnumerable b, string message = null)
        {
            var left = List(a);
            var right = List(b);

            if (left.Count != right.Count)
                throw new AssertionException(Say(message, $"lengths differ: {left.Count} and {right.Count}"));

            var remaining = new List<object>(right);
            foreach (var item in left)
            {
                int at = remaining.FindIndex(candidate => Numbers.Equal(candidate, item, 0d));
                if (at < 0) throw new AssertionException(Say(message, $"{Numbers.Show(item)} is missing"));
                remaining.RemoveAt(at);
            }
        }

        public static void Contains(IEnumerable a, object item, string message = null)
        {
            if (!Assert.Has(a, item))
                throw new AssertionException(Say(message, $"{Numbers.Show(item)} was not in the collection"));
        }

        public static void DoesNotContain(IEnumerable a, object item, string message = null)
        {
            if (Assert.Has(a, item))
                throw new AssertionException(Say(message, $"{Numbers.Show(item)} was in the collection"));
        }

        public static void IsEmpty(IEnumerable a, string message = null)
        {
            if (Assert.Count(a) != 0)
                throw new AssertionException(Say(message, $"expected nothing, found {Assert.Count(a)}"));
        }

        public static void IsNotEmpty(IEnumerable a, string message = null)
        {
            if (Assert.Count(a) == 0) throw new AssertionException(Say(message, "expected something, found nothing"));
        }

        public static void AllItemsAreUnique(IEnumerable a, string message = null)
        {
            var seen = new List<object>();
            foreach (var item in a)
            {
                if (seen.Exists(candidate => Numbers.Equal(candidate, item, 0d)))
                    throw new AssertionException(Say(message, $"{Numbers.Show(item)} appears twice"));
                seen.Add(item);
            }
        }

        public static void AreNotEqual(IEnumerable a, IEnumerable b, string message = null)
        {
            try { AreEqual(a, b); }
            catch (AssertionException) { return; }
            throw new AssertionException(Say(message, "the collections are equal"));
        }

        static List<object> List(IEnumerable items)
        {
            var list = new List<object>();
            if (items != null) foreach (var item in items) list.Add(item);
            return list;
        }

        static string Say(string message, string what) => message == null ? what : $"{message}\n  {what}";
    }
}
