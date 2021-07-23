using ModbusHelper.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ModbusHelper
{
   
    public static class  ModbusExtend
    {
        public enum GetModbusAddressType
        {
            REG_ALL,
            REG_READONLY,
            REG_READWRITE,
        }
        public static List<string> GenerateModbusAddressList(this Modbus modbus, GetModbusAddressType getType, string spliteStr = "--------------")
        {

            switch (getType)
            {
                case GetModbusAddressType.REG_READONLY:
                    return GetRegAddressList(modbus.ReadOnlyReg, spliteStr);
                case GetModbusAddressType.REG_READWRITE:
                    return GetRegAddressList(modbus.ReadWriteReg, spliteStr);
                default:
                    var result = GetRegAddressList(modbus.ReadOnlyReg, spliteStr);
                    result.AddRange(GetRegAddressList(modbus.ReadWriteReg, spliteStr));
                    return result;
            }
        }
        private static List<string> GetRegAddressList(List<AtomModel> src, string spliteStr = "--------------")
        {
            List<string> addressLst = new List<string>();
            if (src?.Count > 0)
            {
                foreach (var tmp in src)
                {
                    addressLst.Add(tmp.RegAddress.ToString() + spliteStr + tmp.Name);
                }
            }
            return addressLst;
        }
    }
}
