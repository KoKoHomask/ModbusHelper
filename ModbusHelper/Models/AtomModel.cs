using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ModbusHelper.Models
{
    public enum ModbusStatus
    {
        OK=0,
        ERR,
    }
    public enum BITORDER
    {
        A = 0,
        ABCD,
        CDAB,
        BADC,
        DCBA,
        ABCDEFGH,
        GHEFCDAB,
        BADCFEHG,
        HGFEDCBA,
    }

    public class AtomModel
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 顺序
        /// </summary>
        public BITORDER BitOrder { get; set; }

        private byte[] lastDataArray = new byte[8];
        private byte[] dataArray = new byte[8];//数据数组
        private int regAddress = 30001;
        public Func<AtomModel, Task<ModbusStatus>> setValueEventhandler { get; set; }
        /// <summary>
        /// 寄存器地址
        /// </summary>
        public int RegAddress
        {
            get => regAddress;
            set
            {
                if (value <= 9999)
                    regAddress = 30000 + value;
                else if (value > 30000 && value < 40000)
                    regAddress = value;
                else if (value > 40000 && value < 50000)
                    regAddress = value;
                else
                    throw new Exception("error modbus address");
            }
        }
        /// <summary>
        /// 内存中的位置
        /// </summary>
        public int MemAdd
        {
            get
            {
                int idx = 0;
                if (regAddress > 30000 && regAddress < 40000)
                    idx = RegAddress - 30000;
                if (regAddress > 40000 && regAddress < 50000)
                    idx = RegAddress - 40000;
                return (idx - 1) * 2;
            }
        }
        /// <summary>
        /// 只读
        /// </summary>
        public bool ReadOnly
        {
            get
            {
                if (this.RegAddress > 40000)
                    return false;
                return true;
            }
        }
        /// <summary>
        /// modbus寄存器长度(字)
        /// </summary>
        public int ModbusLen { get => DataMemLen / 2; }
        /// <summary>
        /// 实际内存长度(字节)
        /// </summary>
        public int DataMemLen
        {
            get
            {
                switch (this.BitOrder)
                {
                    case BITORDER.ABCD:
                    case BITORDER.BADC:
                    case BITORDER.CDAB:
                    case BITORDER.DCBA:
                        return 4;
                    case BITORDER.ABCDEFGH:
                    case BITORDER.BADCFEHG:
                    case BITORDER.GHEFCDAB:
                    case BITORDER.HGFEDCBA:
                        return 8;
                    default:
                        return 2;
                }
            }
        }
        /// <summary>
        /// 获取数据
        /// </summary>
        /// <returns></returns>
        public byte[] GetValue()
        {
            byte[] array = new byte[this.DataMemLen];
            Array.Copy(dataArray, array, array.Length);
            return array;
        }

        public void SetValue(Int16 val)
        {
            BaseSetValue2(BitConverter.GetBytes(val));
        }
        public void SetValue(UInt16 val)
        {
            BaseSetValue2(BitConverter.GetBytes(val));
        }
        public void SetValue(byte high, byte low)
        {
            dataArray[0] = high;
            dataArray[1] = low;
        }
        public void SetValue(double val)
        {
            BaseSetValue8(BitConverter.GetBytes(val));
        }
        public void SetValue(float val)
        {
            BaseSetValue4(BitConverter.GetBytes(val));
        }
        public void SetValue(Int32 val)
        {
            BaseSetValue4(BitConverter.GetBytes(val));
        }

        /// <summary>
        /// modbus向寄存器写数据时
        /// </summary>
        /// <param name="datas">数据</param>
        /// <param name="memAddStart">内存起始位置</param>
        /// <param name="dataLen">数据长度</param>
        /// <param name="runEvent">执行改变事件</param>
        /// <returns>true:需要执行寄存器改变响应事件</returns>
        public async Task<ModbusStatus> SetValueFromModbus(byte[] datas, int memAddStart, int dataLen)
        {
            //范围判定
            if (MemAdd + DataMemLen <= memAddStart)
                return ModbusStatus.OK;
            if (MemAdd >= memAddStart + dataLen)
                return ModbusStatus.OK;
            Array.Copy(dataArray, lastDataArray, dataArray.Length);
            for (int i = 0; i < dataLen; i++)
            {
                if (i + memAddStart < MemAdd + DataMemLen)
                {
                    int idx = i + memAddStart - MemAdd;
                    if (idx < 0) continue;
                    dataArray[idx] = datas[i];
                }
            }
            ModbusStatus status = ModbusStatus.OK;
            if(setValueEventhandler!=null)
                status = await setValueEventhandler?.Invoke(this);
            return status;
        }
        public void BackToLastData()
        {
            Array.Copy(lastDataArray, dataArray, dataArray.Length);
        }
        private void BaseSetValue2(byte[] bytes)
        {
            dataArray[0] = bytes[1];
            dataArray[1] = bytes[0];
        }

        private void BaseSetValue4(byte[] bytes)
        {
            switch (this.BitOrder)
            {
                case BITORDER.ABCD:
                    dataArray[0] = bytes[3];
                    dataArray[1] = bytes[2];
                    dataArray[2] = bytes[1];
                    dataArray[3] = bytes[0];
                    break;
                case BITORDER.BADC:
                    dataArray[0] = bytes[2];
                    dataArray[1] = bytes[3];
                    dataArray[2] = bytes[0];
                    dataArray[3] = bytes[1];
                    break;
                case BITORDER.CDAB:
                    dataArray[0] = bytes[1];
                    dataArray[1] = bytes[0];
                    dataArray[2] = bytes[3];
                    dataArray[3] = bytes[2];
                    break;
                case BITORDER.DCBA:
                    dataArray[0] = bytes[0];
                    dataArray[1] = bytes[1];
                    dataArray[2] = bytes[2];
                    dataArray[3] = bytes[3];
                    break;
            }
        }

        private void BaseSetValue8(byte[] bytes)
        {
            switch (this.BitOrder)
            {
                case BITORDER.ABCDEFGH:
                    dataArray[0] = bytes[7];
                    dataArray[1] = bytes[6];
                    dataArray[2] = bytes[5];
                    dataArray[3] = bytes[4];
                    dataArray[4] = bytes[3];
                    dataArray[5] = bytes[2];
                    dataArray[6] = bytes[1];
                    dataArray[7] = bytes[0];
                    break;
                case BITORDER.BADCFEHG:
                    dataArray[0] = bytes[6];
                    dataArray[1] = bytes[7];
                    dataArray[2] = bytes[4];
                    dataArray[3] = bytes[5];
                    dataArray[4] = bytes[2];
                    dataArray[5] = bytes[3];
                    dataArray[6] = bytes[0];
                    dataArray[7] = bytes[1];
                    break;
                case BITORDER.GHEFCDAB:
                    dataArray[0] = bytes[1];
                    dataArray[1] = bytes[0];
                    dataArray[2] = bytes[3];
                    dataArray[3] = bytes[2];
                    dataArray[4] = bytes[5];
                    dataArray[5] = bytes[4];
                    dataArray[6] = bytes[7];
                    dataArray[7] = bytes[6];
                    break;
                case BITORDER.HGFEDCBA:
                    dataArray[0] = bytes[0];
                    dataArray[1] = bytes[1];
                    dataArray[2] = bytes[2];
                    dataArray[3] = bytes[3];
                    dataArray[4] = bytes[4];
                    dataArray[5] = bytes[5];
                    dataArray[6] = bytes[6];
                    dataArray[7] = bytes[7];
                    break;
            }
        }
    }
}
