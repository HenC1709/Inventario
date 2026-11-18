using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Base ViewModel providing shared <see cref="INotifyDataErrorInfo"/> validation state and notifications.
/// </summary>
public abstract class ValidatableViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>
    /// Occurs when validation errors change for a property or for the entire entity.
    /// </summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>
    /// Gets a value indicating whether any property currently has validation errors.
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// Returns the validation errors for the given property, or all errors when propertyName is null or empty.
    /// </summary>
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.Values.SelectMany(list => list).ToList();

        return _errors.TryGetValue(propertyName, out var errors)
            ? errors
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Sets or clears the validation error message for a single property and raises <see cref="ErrorsChanged"/>.
    /// </summary>
    protected void SetPropertyError(string propertyName, string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            if (_errors.Remove(propertyName))
                OnErrorsChanged(propertyName);
            return;
        }

        _errors[propertyName] = new List<string> { errorMessage };
        OnErrorsChanged(propertyName);
    }

    /// <summary>
    /// Raises the <see cref="ErrorsChanged"/> and <see cref="HasErrors"/> notifications.
    /// </summary>
    protected void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }
}
