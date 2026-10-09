using System.Windows.Data;
using System.Windows.Markup;
using QBitRemote.Services;

namespace QBitRemote.Infrastructure;

public sealed class LocExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{Key}]") { Source = LocalizationService.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
