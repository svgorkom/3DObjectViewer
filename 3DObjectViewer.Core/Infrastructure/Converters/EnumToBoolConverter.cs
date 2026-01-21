using System.Globalization;
using System.Windows.Data;

namespace _3DObjectViewer.Core.Infrastructure.Converters;

/// <summary>
/// A value converter that enables two-way binding between enum values and boolean states,
/// typically used for binding enum properties to RadioButton controls.
/// </summary>
/// <remarks>
/// <para>
/// This converter compares an enum value to a parameter and returns <see langword="true"/>
/// if they match, enabling RadioButton-style selection for enum properties.
/// </para>
/// <para>
/// Use the <c>ConverterParameter</c> to specify which enum value the RadioButton represents.
/// </para>
/// </remarks>
/// <example>
/// XAML usage:
/// <code>
/// &lt;RadioButton Content="Red" 
///              IsChecked="{Binding SelectedColor, 
///                          Converter={StaticResource EnumToBoolConverter}, 
///                          ConverterParameter=Red}"/&gt;
/// </code>
/// </example>
public class EnumToBoolConverter : IValueConverter
{
    /// <summary>
    /// Converts an enum value to a boolean by comparing it with the converter parameter.
    /// </summary>
    /// <param name="value">The enum value from the binding source.</param>
    /// <param name="targetType">The type of the binding target property (typically <see cref="bool"/>).</param>
    /// <param name="parameter">
    /// The enum value to compare against, specified as a string matching the enum member name.
    /// </param>
    /// <param name="culture">The culture to use in the converter (not used).</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> matches <paramref name="parameter"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        return value.ToString() == parameter.ToString();
    }

    /// <summary>
    /// Converts a boolean value back to an enum value when the RadioButton is checked.
    /// </summary>
    /// <param name="value">The boolean value from the binding target (IsChecked property).</param>
    /// <param name="targetType">The type of the binding source property (the enum type).</param>
    /// <param name="parameter">
    /// The enum value to return when <paramref name="value"/> is <see langword="true"/>,
    /// specified as a string matching the enum member name.
    /// </param>
    /// <param name="culture">The culture to use in the converter (not used).</param>
    /// <returns>
    /// The parsed enum value if <paramref name="value"/> is <see langword="true"/>;
    /// otherwise, <see cref="Binding.DoNothing"/> to prevent updating the source.
    /// </returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is not null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }

        return Binding.DoNothing;
    }
}
