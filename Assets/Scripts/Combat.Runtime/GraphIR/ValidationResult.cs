using System;
using System.Collections.Generic;

namespace Combat.Runtime.GraphIR
{
    public sealed class ValidationResult
    {
        private readonly List<ValidationError> _errors;

        public bool isValid => _errors.Count == 0;
        public IReadOnlyList<ValidationError> errors => _errors;

        public ValidationResult()
        {
            _errors = new List<ValidationError>(4);
        }

        public void AddError(string nodeId, string message)
        {
            _errors.Add(new ValidationError(nodeId, message ?? string.Empty));
        }
    }
}

