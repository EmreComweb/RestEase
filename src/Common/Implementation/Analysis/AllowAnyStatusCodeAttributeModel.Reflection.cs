using System;

namespace RestEase.Implementation.Analysis
{
    internal partial class AllowAnyStatusCodeAttributeModel
    {
        public Type ContainingType { get; }

        public AllowAnyStatusCodeAttributeModel(AllowAnyStatusCodeAttribute attribute, Type containingType)
            : base(attribute)
        {
            this.ContainingType = containingType;
        }

        public bool IsDeclaredOn(TypeModel typeModel) => this.ContainingType == typeModel.Type;
    }
}