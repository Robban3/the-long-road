// A stand-in for NUnit, just enough to type-check the EditMode tests outside Unity.
// It asserts nothing: the point is to catch a test that will not compile, which has
// twice now been discovered by a person opening the editor.
using System;
using System.Collections;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }

    public class Constraint { }

    public static class Is
    {
        public static Constraint EqualTo(object v) => null;
        public static Constraint InRange(object a, object b) => null;
        public static Constraint GreaterThan(object v) => null;
        public static Constraint GreaterThanOrEqualTo(object v) => null;
        public static Constraint LessThan(object v) => null;
        public static Constraint LessThanOrEqualTo(object v) => null;
        public static Constraint True => null;
        public static Constraint False => null;
        public static Constraint Not => null;
        public static Constraint Null => null;
    }

    public static class ConstraintExtensions
    {
        public static Constraint Within(this Constraint c, object tolerance) => c;
        public static Constraint Percent(this Constraint c) => c;
    }

    public static class Assert
    {
        public static void That(object actual, Constraint c, string message = null) { }
        public static void That(bool condition, string message = null) { }
        public static void AreEqual(object a, object b, string message = null) { }
        public static void AreEqual(double a, double b, double delta, string message = null) { }
        public static void AreNotEqual(object a, object b, string message = null) { }
        public static void IsTrue(bool c, string message = null) { }
        public static void IsFalse(bool c, string message = null) { }
        public static void IsNull(object o, string message = null) { }
        public static void IsNotNull(object o, string message = null) { }
        public static void Greater(IComparable a, IComparable b, string message = null) { }
        public static void GreaterOrEqual(IComparable a, IComparable b, string message = null) { }
        public static void Less(IComparable a, IComparable b, string message = null) { }
        public static void LessOrEqual(IComparable a, IComparable b, string message = null) { }
        public static void Fail(string message = null) { }
        public static void Pass(string message = null) { }
        public static void Ignore(string message = null) { }
        public static void AreSame(object a, object b, string message = null) { }
        public static void AreNotSame(object a, object b, string message = null) { }
        public static void IsEmpty(IEnumerable a, string message = null) { }
        public static void IsNotEmpty(IEnumerable a, string message = null) { }
        public static void IsEmpty(string a, string message = null) { }
        public static void Contains(object item, ICollection collection, string message = null) { }
        public static void IsInstanceOf<T>(object o, string message = null) { }
        public static void Throws<T>(Action code, string message = null) { }
        public static void DoesNotThrow(Action code, string message = null) { }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable a, IEnumerable b, string message = null) { }
        public static void AreEquivalent(IEnumerable a, IEnumerable b, string message = null) { }
        public static void Contains(IEnumerable a, object item, string message = null) { }
        public static void IsEmpty(IEnumerable a, string message = null) { }
        public static void IsNotEmpty(IEnumerable a, string message = null) { }
        public static void AllItemsAreUnique(IEnumerable a, string message = null) { }
        public static void DoesNotContain(IEnumerable a, object item, string message = null) { }
        public static void AreNotEqual(IEnumerable a, IEnumerable b, string message = null) { }
    }
}
