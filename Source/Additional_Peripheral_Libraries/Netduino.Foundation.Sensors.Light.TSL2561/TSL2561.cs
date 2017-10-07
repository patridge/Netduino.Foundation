﻿using System;
using Microsoft.SPOT.Hardware;
using Netduino.Foundation.Core;
using Microsoft.SPOT;

namespace Netduino.Foundation.Sensors.Light
{
    /// <summary>
    /// Driver for the TSL2561 light-to-digital converter.
    /// </summary>
    public class TSL2561 : IDisposable
    {
        #region Constants
        
        /// <summary>
        /// The command bit in the Command Register.
        /// 
        /// See page 13 of the datasheet.
        /// </summary>
        private byte COMMAND_BIT = 0x80;

        /// <summary>
        /// The interrupt clear bit in the Command Register.
        /// 
        /// See page 13 of the datasheet.
        /// </summary>
        private byte CLEAR_INTERRUPT_BIT = 0xC0;

        /// <summary>
        /// This bit control the write operations for the TSL2561.  Setting
        /// this bit puts the chip into Word mode for the specified register.
        /// </summary>
        /// <remarks>
        /// See page 13 of the data sheet.
        /// </remarks>
        private byte WORD_MODE_BIT = 0x20;

        #endregion Constants

        #region Enums

        /// <summary>
        /// Valid addresses for the sensor.
        /// </summary>
        public enum Addresses : byte { Default = 0x39, Address0 = 0x29, Address1 = 0x49 };

        /// <summary>
        /// Integration timing.
        ///
        /// See Timing Register on page 14 of the data sheet.
        /// </summary>
        /// <remarks>
        /// Valid integration times are 13.7ms, 101ms, 402ms and Manual.
        /// </remarks>
        public enum IntegrationTiming : byte { Ms13 = 0, Ms101, Ms402, Manual };

        /// <summary>
        /// Possible gain setting for the sensor.
        ///
        /// See Timing Register on page 14 of the data sheet.
        /// </summary>
        /// <remarks>
        /// Possible gain values are low (x1) and high (x16).
        /// </remarks>
        public enum Gain { Low, High };

        /// <summary>
        /// TSL2561 register locations.
        ///
        /// See Register Set on page 12 and Command Register on page 13 of the datasheet.
        /// </summary>
        /// <remarks>
        /// All of the register numbers have 0x80 added to the register.  When reading
        /// or witing to a register the application must set the CMD bit in the command 
        /// register (see page 13) and the register address is written into the lower
        /// four bits of the Command Register.
        /// </remarks>
        public enum Registers : byte
        {
            Control = 0x80,
            Timing = 0x81,
            ThresholdLow = 0x82,
            ThresholdHigh = 0x84,
            InterruptControl = 0x86,
            ID = 0x8a,
            Data0 = 0x8c,
            Data1 = 0x8e
        }

        /// <summary>
        /// Determine if interrupts are enabled or not.
        ///
        /// See Interrupt Control Register on page 15 of the datasheet.
        /// </summary>
        public enum InterruptMode : byte { Disable = 0, Enable };

        #endregion Enums

        #region Member variables / fields.

        /// <summary>
        /// GPIO pin on the Netduino that is connected to the interrupt pin on the TSL2561.
        /// </summary>
        InterruptPort _interruptPin = null;

        #endregion Member variables / fields.

        #region Properties

        /// <summary>
        /// Get the sensor reading
        /// </summary>
        /// <remarks>
        /// This can be used to get the raw sensor data from the TSL2561. 
        /// </remarks>
        /// <returns>Sensor data.</returns>
        public ushort[] SensorReading
        {
            get
            {
                return(_tsl2561.ReadUShorts((byte) Registers.Data0, 2, ByteOrder.LittleEndian));
            }
        }

        /// <summary>
        /// Lux reading from the TSL2561 sensor.
        /// </summary>
        public double Lux
        {
            get
            {
                ushort[] adcData = SensorReading;
                ushort data0 = adcData[0];
                ushort data1 = adcData[1];
                if ((data0 == 0xffff) | (data1 == 0xffff))
                {
                    return(0.0);
                }
                double d0 = data0;
                double d1 = data1;
                double ratio = d1 / d0;

                int milliseconds = 0;
                switch (Timing)
                {
                    case IntegrationTiming.Ms13:
                        milliseconds = 14;
                        break;
                    case IntegrationTiming.Ms101:
                        milliseconds = 101;
                        break;
                    case IntegrationTiming.Ms402:
                        milliseconds = 402;
                        break;
                    case IntegrationTiming.Manual:
                        milliseconds = 0;
                        break;
                }
                double result = 0.0;
                if (milliseconds != 0)
                {
                    d0 *= (402.0 / milliseconds);
                    d1 *= (402.0 / milliseconds);
                    if (SensorGain == Gain.Low)
                    {
                        d0 *= 16;
                        d1 *= 16;
                    }
                    if (ratio < 0.5)
                    {
                        result = 0.0304 * d0 - 0.062 * d0 * System.Math.Pow(ratio, 1.4);
                    }
                    else
                    {
                        if (ratio < 0.61)
                        {
                            result = 0.0224 * d0 - 0.031 * d1;
                        }
                        else
                        {
                            if (ratio < 0.80)
                            {
                                result = 0.0128 * d0 - 0.0153 * d1;
                            }
                            else
                            {
                                if (ratio < 1.30)
                                {
                                    result = 0.00146 * d0 - 0.00112 * d1;
                                }
                            }
                        }
                    }
                }
                return(result);
            }
        }

        /// <summary>
        /// ID of the sensor.
        /// </summary>
        /// <remarks>
        /// The ID register (page 16 of the datasheet) gives two pieces of the information:
        ///     Part Number: bits 4-7 (0000 = TSL2560, 0001 = TSL2561)
        ///     Revision number: bits 0-3
        /// </remarks>
        public byte ID
        {
            get
            {
                return(_tsl2561.ReadRegister((byte) Registers.ID));
            }
        }

        /// <summary>
        /// Gain of the sensor.
        /// 
        /// The sensor gain can be set to high or low.
        /// </summary>
        /// <remarks>
        /// The sensor Gain bit can be found in the Timing Register.  This allows the gain
        /// to be set to High (16x) or Low (1x).
        /// 
        /// See page 14 of the datasheet.
        /// </remarks>
        public Gain SensorGain
        {
            get
            {
                byte data = (byte) (_tsl2561.ReadRegister((byte) Registers.Timing) & 0x10);
                return (data == 0 ? Gain.Low : Gain.High);
            }
            set
            {
                byte data = _tsl2561.ReadRegister((byte) Registers.Timing);
                if (value == Gain.Low)
                {
                    data &= 0xef;       // Set bit 4 to 0.
                }
                else
                {
                    data |= 0x10;       // Set bit 4 to 1.
                }
                _tsl2561.WriteRegister((byte) Registers.Timing, data);
            }
        }

        /// <summary>
        /// Integration timing for the sensor reading.
        /// </summary>
        public IntegrationTiming Timing
        {
            get
            {
                byte timing = _tsl2561.ReadRegister((byte) Registers.Timing);
                timing &= 0x03;
                return ((IntegrationTiming) timing);
            }
            set
            {
                sbyte timing = (sbyte) _tsl2561.ReadRegister((byte) Registers.Timing);
                if (SensorGain == Gain.High)
                {
                    timing |= 0x10;
                }
                else
                {
                    timing &= ~0x10;
                }
                timing &= ~ 0x03;
                timing |= (sbyte) ((sbyte) value & 0x03);
                _tsl2561.WriteRegister((byte) Registers.Timing, (byte) timing);
            }
        }

        /// <summary>
        /// Lower interrupt threshold.
        /// </summary>
        /// <remarks>
        /// Get or se the lower interrupt threshold.  Any readings below this
        /// value may trigger an interrupt <seealso cref="SetInterruptMode"/>
        /// 
        /// See page 14/15 of the datasheet.
        /// </remarks>
        public ushort ThresholdLow
        {
            get
            {
                return (_tsl2561.ReadUShort((byte) Registers.ThresholdLow, ByteOrder.LittleEndian));
            }
            set
            {
                _tsl2561.WriteUShort((byte) (Registers.ThresholdLow + WORD_MODE_BIT), value, ByteOrder.LittleEndian);
            }
        }

        /// <summary>
        /// High interrupt threshold.
        /// </summary>
        /// <remarks>
        /// Get or se the upper interrupt threshold.  Any readings above this
        /// value may trigger an interrupt <seealso cref="SetInterruptMode"/>
        /// 
        /// See page 14/15 of the datasheet.
        /// </remarks>
        public ushort ThresholdHigh
        {
            get
            {
                return (_tsl2561.ReadUShort((byte) Registers.ThresholdHigh, ByteOrder.LittleEndian));
            }
            set
            {
                _tsl2561.WriteUShort((byte) (Registers.ThresholdHigh + WORD_MODE_BIT), value, ByteOrder.LittleEndian);
            }
        }

        /// <summary>
        /// ICommunicationBus object used to communicate with the sensor.
        /// </summary>
        /// <remarks>
        /// In this case the actual object will always be an I2SBus object.
        /// </remarks>
        private ICommunicationBus _tsl2561 = null;

        #endregion Properties

        #region Event definitions

        /// <summary>
        /// Allow the user to attach an interrupt to the TSL2561.
        /// </summary>
        /// <remarks>
        /// This interrupt requires the interrupts to be set up correctly.
        /// <see cref="SetInterruptMode"/>
        /// </remarks>
        /// <param name="time">Date and time the interrupt was generated.</param>
        public delegate void ThresholdInterrupt(DateTime time);

        /// <summary>
        /// Interrupt generated when the reading is outside of the threshold window.
        /// </summary>
        /// <remarks>
        /// This interrupt requires the threshold window to be defined <see cref="SetInterruptMode"/>
        /// and for the interrupts to be enabled.
        /// </remarks>
        public event ThresholdInterrupt ReadingOutsideThresholdWindow = null;

        #endregion Event definitions

        #region Constructor(s)

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// Default constructor is private to force the setting of the I2C parameters.
        /// </remarks>
        private TSL2561()
        {
        }

        /// <summary>
        /// Create a new instance of the TSL2561 class with the specified I2C address.
        /// </summary>
        /// <remarks>
        /// By default the sensor will be set to low gain.
        /// <remarks>
        public TSL2561(byte address = (byte) Addresses.Default, ushort speed = 100)
        {
            if ((address != (byte) Addresses.Address0) && (address != (byte) Addresses.Default) && (address != (byte) Addresses.Address1))
            {
                throw new ArgumentOutOfRangeException("Address", "Address should be 0x29, 0x39 or 0x49.");
            }
            I2CBus device = new I2CBus(address, speed);
            _tsl2561 = (ICommunicationBus) device;
        }

        #endregion Constructor(s)

        #region IDisposable

        /// <summary>
        /// Implement IDisposable interface.
        /// </summary>
        public void Dispose()
        {
            if (_interruptPin != null)
            {
                _interruptPin.Dispose();
            }
        }

        #endregion IDisposable

        #region Methods

        /// <summary>
        /// Turn the TSL2561 off.
        /// <summary>
        /// <remarks>
        /// Reset the power bits in the control register (page 13 of the datasheet).
        /// </remarks>
        public void TurnOff()
        {
            _tsl2561.WriteRegister((byte) Registers.Control, 0x00);
        }

        /// <summary>
        /// Turn the TSL2561 on.
        /// </summary>
        /// <remarks>
        /// Set the power bits in the control register (page 13 of the datasheet).
        /// </remarks>
        public void TurnOn()
        {
            _tsl2561.WriteRegister((byte) Registers.Control, 0x03);
        }

        /// <summary>
        /// Clear the interrupt flag.
        /// 
        /// Se Command Register on page 13 of the datasheet.
        /// </summary>
        /// <remarks>
        /// According to the datasheet, writing a 1 into bit 6 of the command 
        /// register will clear any pending interrupts.
        /// </remarks>
        public void ClearInterrupt()
        {
            _tsl2561.WriteByte(CLEAR_INTERRUPT_BIT);
            if (_interruptPin != null)
            {
                _interruptPin.ClearInterrupt();
            }
        }

        /// <summary>
        /// Put the sensor into manual integration mode.
        /// </summary>
        public void ManualStart()
        {
            byte timing = _tsl2561.ReadRegister((byte) Registers.Timing);
            timing |= 0x03;
            _tsl2561.WriteRegister((byte) Registers.Timing, timing);
            timing |= 0xf7; //  ~0x08;
            _tsl2561.WriteRegister((byte) Registers.Timing, timing);
        }

        /// <summary>
        /// Turn off manual integration mode.
        /// </summary>
        public void ManualStop()
        {
            byte timing = _tsl2561.ReadRegister((byte) Registers.Timing);
            timing &= 0xf7; //  ~0x08;
            _tsl2561.WriteRegister((byte) Registers.Timing, timing);
        }

        /// <summary>
        /// Turn interrupts on or off and set the conversion trigger count.
        /// </summary>
        /// <remarks>
        /// The conversion count is the number of conversions that must be outside
        /// of the upper and lower limits before and interrupt is generated.
        /// 
        /// See Interrupt Control Register on page 15 and 16 of the datasheet.
        /// </remarks>
        /// <param name="mode"></param>
        /// <param name="conversionCount">Number of conversions that must be outside of the threshold before an interrupt is generated.</param>
        /// <param name="pin">GPIO pin connected to the TSL2561 interrupt pin.  Set to null to use the previously supplied pin.</param>
        public void SetInterruptMode(InterruptMode mode, byte conversionCount, Cpu.Pin pin = Cpu.Pin.GPIO_NONE)
        {
            if (conversionCount > 15)
            {
                throw new ArgumentOutOfRangeException("conversionCount", "Conversion count must be in the range 0-15 inclusive.");
            }
            //
            //  Attach the interrupt event before we turn on interrupts.
            //
            if (pin != Cpu.Pin.GPIO_NONE)
            {
                if (_interruptPin != null)
                {
                    _interruptPin.Dispose();
                }
                _interruptPin = new InterruptPort(pin, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
                _interruptPin.OnInterrupt += _interruptPin_OnInterrupt;
            }
            else
            {
                if (_interruptPin == null)
                {
                    throw new ArgumentException("Interrupt pin must be supplied");
                }
            }
            //
            // Put interrupt control in bits 4 & 5 of the Interrupt Control Register.
            // Using the enum above makes sure that mode is in the range 0-3 inclusive.
            //
            byte registerValue = (byte) mode;
            registerValue <<= 4;
            //
            // conversionCount is known to be 0-15, put this in the lower four bits of
            // the Interrupt Control Register.
            //
            registerValue |= conversionCount;
            //
            //  Clear the interrupt bit before we turn them on.
            //
            ClearInterrupt();
            _tsl2561.WriteRegister((byte) Registers.InterruptControl, registerValue);
        }

        public void RegisterContents()
        {
            byte[] registers = _tsl2561.ReadRegisters((byte) Registers.Control, 16);
            for (int index = 0; index < 16; index++)
            {
                Debug.Print("Register " + index.ToString() + " : " + registers[index].ToString());
            }
        }

        #endregion Methods

        #region Interrupt handlers

        /// <summary>
        /// Process the interrupt generated by the TSL2561.
        /// </summary>
        /// <param name="data1"></param>
        /// <param name="data2"></param>
        /// <param name="time">Date and time of the interrupt.</param>
        void _interruptPin_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            if (ReadingOutsideThresholdWindow != null)
            {
                ReadingOutsideThresholdWindow(time);
            }
        }

        #endregion Interrupt handlers
    }
}
