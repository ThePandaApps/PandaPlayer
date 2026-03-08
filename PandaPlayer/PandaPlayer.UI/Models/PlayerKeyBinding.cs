using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace PandaPlayer.UI.Models
{
    public class PlayerKeyBinding
    {
        public string ActionId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Key Key { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModifierKeys Modifiers { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            
            // Handle number keys specially for nice display
            string k = Key.ToString();
            if (k.StartsWith("D") && k.Length == 2 && char.IsDigit(k[1])) k = k[1].ToString();
            else if (k.StartsWith("NumPad")) k = "Num" + k.Substring(6);
            else if (k == "OemQuestion") k = "?";
            else if (k == "OemPeriod") k = ".";
            else if (k == "OemComma") k = ",";
            
            parts.Add(k);
            return string.Join("+", parts);
        }
    }
}
