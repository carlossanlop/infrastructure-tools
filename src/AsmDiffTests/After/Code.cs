// After
using System;

namespace AsmDiffTests
{
    [MyAttribute]
    public class MyClass
    {
    }

    public class MyAttributeAttribute : Attribute
    {
        public MyAttributeAttribute() { }
    }
}