using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OrbyLauncher.Services;

internal static class PeripheralBatteryChecks
{
    internal static int Run(string[] args)
    {
        int count = 0;
        void Check(bool ok, string name)
        { if (!ok) throw new InvalidOperationException(name); count++; Console.WriteLine("PASS " + name); }
        try
        {
            var voltage = new ChargeRiseDetector(120, 80);
            Check(!voltage.Update(3950, 0), "Primeira leitura alta não presume carregador");
            Check(!voltage.Update(3960, 10000), "Oscilação pequena não indica carga");
            Check(!voltage.Update(4120, 20000), "Salto isolado aguarda confirmação");
            Check(voltage.HoldBaseline && voltage.Baseline == 3960, "Primeiro salto já impede mostrar porcentagem inflada");
            Check(voltage.Update(4130, 30000) && voltage.Baseline == 3960, "Salto confirmado preserva nível anterior");
            Check(!voltage.Update(4000, 40000), "Queda de tensão desfaz estimativa");
            var glitch = new ChargeRiseDetector(120, 80);
            glitch.Update(3800, 0); glitch.Update(4000, 10000);
            Check(!glitch.Update(3810, 20000), "Pico transitório não confirma carga");
            var keyboard = new ChargeRiseDetector(20, 10);
            keyboard.Update(50, 0); keyboard.Update(80, 10000);
            Check(keyboard.Update(80, 20000), "Teclado: salto repetido permite estimativa");
            Check(!keyboard.Update(80, 150000), "Leitura após lacuna perde confirmação antiga");
            var expiry = new ChargeRiseDetector(20, 10);
            expiry.Update(50, 0); expiry.Update(80, 10000); expiry.Update(80, 20000);
            for (long t = 30000; t < 620000; t += 10000) expiry.Update(80, t);
            Check(!expiry.Update(80, 620000), "Estimativa expira sem se perpetuar");
            var reading = new PeripheralBattery("Teclado", 50, "", Charging: true, ChargingEstimated: true);
            Check(!reading.Display.Contains("%") && reading.VisualCharge == 50, "UI oculta percentual inflado e preserva barra");
            string card = "{\"type\":\"AdaptiveCard\",\"speak\":\"Telefone - Conectado ao computador\",\"body\":[{\"type\":\"Image\",\"tooltip\":\"Bateria com 37% restante\"}]}";
            var phone = PhoneLinkBattery.Parse(System.Text.Encoding.UTF8.GetBytes(card));
            Check(phone.Length == 1 && phone[0].Percent == 37, "Phone Link: cartão português com bateria real");
            Check(PhoneLinkBattery.Parse(System.Text.Encoding.UTF8.GetBytes(card.Replace("Conectado ao computador", "Desconectado"))).Length == 0, "Phone Link: desconectado não produz leitura");
            Check(PhoneLinkBattery.Parse(System.Text.Encoding.UTF8.GetBytes(card.Replace("37%", "137%"))).Length == 0, "Phone Link: rejeita nível inválido");
            Check(PhoneLinkBattery.Parse(System.Text.Encoding.UTF8.GetBytes(card.Replace("Image", "Action.Submit"))).Length == 0, "Phone Link: não busca bateria em ações");
            var preferences = new Preferences(); preferences.HiddenBatteryDevices.Add("Teclado"); preferences.BatteryDeviceIcons["Teclado"] = "mouse";
            PeripheralPresentation.Preferences = preferences;
            Check(PeripheralPresentation.Hidden("Teclado") && PeripheralPresentation.Glyph(reading) == PeripheralPresentation.Icon("mouse"), "Preferências ocultam e corrigem ícone sem alterar leitura");
            PeripheralPresentation.Preferences = null;
            Console.WriteLine($"RESULTADO: {count} aprovados; 0 falhos.");
            if (args.Contains("--live")) Live(args.Last()).GetAwaiter().GetResult();
            if (args.Contains("--keyboard-gatt")) KeyboardGatt(Convert.ToUInt64(args.Last(), 16)).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error); return 1; }
    }
    private static async Task KeyboardGatt(ulong address)
    {
        using var timeout = new System.Threading.CancellationTokenSource(15000);
        using var device = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(timeout.Token);
        if (device is null) { Console.WriteLine("INCONCLUSIVO: teclado Bluetooth indisponível"); return; }
        var services = await device.GetGattServicesForUuidAsync(new Guid("0000180f-0000-1000-8000-00805f9b34fb"), Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(timeout.Token);
        Console.WriteLine("GATT bateria: " + services.Status);
        foreach (var service in services.Services)
        {
            using (service)
            {
                var chars = await service.GetCharacteristicsAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                foreach (var c in chars.Characteristics)
                {
                    Console.WriteLine("Característica: " + c.Uuid + " " + c.CharacteristicProperties);
                    if (c.Uuid != new Guid("00002a19-0000-1000-8000-00805f9b34fb") && c.Uuid != new Guid("00002a1a-0000-1000-8000-00805f9b34fb")) continue;
                    var value = await c.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                    if (value.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success) { Console.WriteLine(value.Status); continue; }
                    using var reader = Windows.Storage.Streams.DataReader.FromBuffer(value.Value);
                    var bytes = new byte[value.Value.Length]; reader.ReadBytes(bytes);
                    Console.WriteLine("Valor GATT: " + Convert.ToHexString(bytes));
                }
            }
        }
    }
    private static async Task Sample(string phase)
    {
        var kb = BluetoothDriverBattery.Read().FirstOrDefault(x => x.Name == "BT+2.4G KB");
        var headset = await G435Battery.Read();
        Console.WriteLine($"{phase} teclado: {kb?.Display ?? "sem leitura"}; barra={kb?.Percent}; estimado={kb?.ChargingEstimated}");
        Console.WriteLine($"{phase} G435: {G435Battery.LastMillivolts?.ToString() ?? "?"}mV; {headset?.Display ?? "sem resposta"}; barra={headset?.Percent}; carga={headset?.Charging}; estimado={headset?.ChargingEstimated}");
    }
    private static async Task Live(string signal)
    {
        await Sample("SEM CABO 1"); await Task.Delay(5000); await Sample("SEM CABO 2");
        Console.WriteLine("PRONTO_PARA_CONECTAR");
        long start = Environment.TickCount64;
        while (!File.Exists(signal) && Environment.TickCount64 - start < 180000) await Task.Delay(500);
        if (!File.Exists(signal)) { Console.WriteLine("INCONCLUSIVO: cabo não confirmado dentro da janela"); return; }
        for (int i = 1; i <= 6; i++) { await Sample("COM CABO " + i); if (i < 6) await Task.Delay(8000); }
        Console.WriteLine("DIAGNOSTICO_CONCLUIDO: comparar dados; isto não confirma por si só carregamento.");
    }
}
