using System.Threading;
using H = Microsoft.SPOT.Hardware;
using N = SecretLabs.NETMF.Hardware.Netduino;
using Netduino.Foundation.Motors;

namespace HBridgeMotor_Sample
{
    public class Program
    {
        public static void Main()
        {
            var motor1 = new HBridgeMotor(N.PWMChannels.PWM_PIN_D3, N.PWMChannels.PWM_PIN_D5, N.Pins.GPIO_PIN_D4);
            var motor2 = new HBridgeMotor(N.PWMChannels.PWM_PIN_D6, N.PWMChannels.PWM_PIN_D10, N.Pins.GPIO_PIN_D7);

            while (true)
            {
                // set the speed on both motors to 100% forward
                motor1.Speed = 1f;
                motor2.Speed = 1f;
                Thread.Sleep(1000);
                motor1.Speed = 0f;
                motor2.Speed = 0f;
                Thread.Sleep(500);
                // 100% reverse
                motor1.Speed = -1f;
                motor2.Speed = -1f;
                Thread.Sleep(1000);
            }
        }
    }
}
