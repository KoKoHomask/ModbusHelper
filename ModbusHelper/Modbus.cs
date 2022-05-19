using ModbusHelper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModbusHelper
{
    public class Modbus
    {
        public List<AtomModel> ReadOnlyReg { get; set; }//只读寄存器
        public List<AtomModel> ReadWriteReg { get; set; }//读写寄存器
        public byte ModbusId { get; set; } = 1;//modbus地址
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="atoms"></param>
        public void InitModbusReg(IEnumerable<AtomModel> atoms, byte modbusId = 1)
        {
            ReadOnlyReg?.Clear();
            ReadWriteReg?.Clear();
            ReadOnlyReg = new List<AtomModel>();
            ReadWriteReg = new List<AtomModel>();
            ModbusId = modbusId;
            var roReg = atoms.Where(x => x.ReadOnly == true).OrderBy(x => x.MemAdd).ToList();
            var rwReg = atoms.Where(x => x.ReadOnly == false).OrderBy(x => x.MemAdd).ToList();
            void checkandinsert(List<AtomModel> src, List<AtomModel> des)
            {
                if (src != null)
                {
                    for (int i = 0; i < src.Count; i++)
                    {
                        if (i != src.Count - 1)
                        {
                            if (src[i].MemAdd + src[i].DataMemLen > src[i + 1].MemAdd)
                                throw new Exception("error reg address in read only Reg");
                        }
                        des.Add(src[i]);
                    }
                }
            }
            checkandinsert(roReg, ReadOnlyReg);
            checkandinsert(rwReg, ReadWriteReg);
        }
        private byte[] processModbusTcp(byte[] cmd,out bool isModbusTcp,out int iD)
        {
            isModbusTcp = false;
            iD = 0;
            if (cmd.Length <= 8)
                return cmd;

            if (cmd[2]==0x00&&cmd[3]==0x00)
            { 
                iD = cmd[0] * 256 + cmd[1];
                int tmpLen = cmd[4] * 256 + cmd[5];
                if (cmd.Length != tmpLen + 6)
                    return cmd;
                byte[] tmpArray = new byte[tmpLen];
                Array.Copy(cmd, 6, tmpArray, 0, tmpLen);
                isModbusTcp = true;
                return tmpArray;
            }
            return cmd;
        }
        private byte[] modbusBackArrayProcess(bool isModbusTcp,byte[] _recieveData,int id)
        {
            if (!isModbusTcp)
                return _recieveData;
            var idTmp = BitConverter.GetBytes((UInt16)id);
            var lenTmp = BitConverter.GetBytes((ushort)(_recieveData.Length - 2));
            
            int dataLen = _recieveData.Length - 2;
            if (_recieveData[1] == 0x06)
                dataLen = _recieveData.Length;
            var backArray = new byte[6 + dataLen];
            
            backArray[0] = idTmp[1];
            backArray[1] = idTmp[0];
            backArray[4] = lenTmp[1];
            backArray[5] = lenTmp[0];
            Array.Copy(_recieveData, 0, backArray, 6, dataLen);
            return backArray;
        }
        /// <summary>
        /// 处理接收事件
        /// </summary>
        /// <param name="cmd"></param>
        public byte[] ProcessRecieveCmd(byte[] _cmd)
        {
            bool isModbusTcp;
            int id;
            var cmd = processModbusTcp(_cmd,out isModbusTcp,out id);
            if (cmd[0] != ModbusId) return null;//modbus 地址错误
            byte[] crc;
            if(!isModbusTcp)
            {
                crc = CRCHelper.CRC16(cmd, cmd.Length - 2);
                if (cmd[cmd.Length - 1] != crc[0] ||//crc错误
                     cmd[cmd.Length - 2] != crc[1])
                    return null;
            }
            
            if (cmd[1] == 0x03 || cmd[1] == 0x04)
            {
                List<AtomModel> srcLst = ReadWriteReg;
                ushort startReg = (ushort)(cmd[2] * 256 + cmd[3] + 40001);
                ushort len = (ushort)(cmd[4] * 256 + cmd[5]);
                if (cmd[1] == 0x04)//要读的是30寄存器
                {
                    startReg = (ushort)(cmd[2] * 256 + cmd[3] + 30001);
                    srcLst = ReadOnlyReg;
                }
                var modbusRegLen = srcLst[srcLst.Count - 1].RegAddress + srcLst[srcLst.Count - 1].ModbusLen;
                if (startReg + len > modbusRegLen)//超出范围
                    return modbusBackArrayProcess(isModbusTcp, backError((byte)(0x80 | cmd[1]), 0x02), id);
                var dataArray = getDataFromReg(srcLst, startReg, len);
                byte[] rebackArray = new byte[len * 2 + 5];
                rebackArray[0] = ModbusId;
                rebackArray[1] = cmd[1];
                rebackArray[2] = (byte)(len * 2);
                Array.Copy(dataArray, 0, rebackArray, 3, dataArray.Length);
                crc = CRCHelper.CRC16(rebackArray, rebackArray.Length - 2);
                rebackArray[rebackArray.Length - 1] = crc[0];
                rebackArray[rebackArray.Length - 2] = crc[1];
                return modbusBackArrayProcess(isModbusTcp, rebackArray, id);
            }

            if (cmd[1] == 0x06)//写单个40寄存器
                return modbusBackArrayProcess(isModbusTcp, modbus06(cmd,isModbusTcp), id);
            if (cmd[1] == 0x10)//写连续40寄存器
                return modbusBackArrayProcess(isModbusTcp, modbus10(cmd,isModbusTcp), id);

            return null;
        }
        private byte[] modbus10(byte[] UBUFP,bool isModbusTcp)
        {
            ushort startRegAdd = (ushort)(UBUFP[2] * 256 + UBUFP[3] + 40001);
            ushort modbusLen = (ushort)(UBUFP[4] * 256 + UBUFP[5]);
            if (!isModbusTcp&& UBUFP.Length < 9 + modbusLen * 2)
                return backError((byte)(UBUFP[1] | 0x80), 0x01);//数据包长度不合法
            if(startRegAdd+modbusLen> ReadWriteReg[ReadWriteReg.Count - 1].RegAddress + ReadWriteReg[ReadWriteReg.Count - 1].ModbusLen)
                return backError((byte)(UBUFP[1] | 0x80), 0x02);//范围有误

            byte[] datas = new byte[modbusLen * 2];
            Array.Copy(UBUFP, 7, datas, 0, datas.Length);
            for (int i = 0; i < ReadWriteReg.Count; i++)
            {
                var reg = ReadWriteReg[i];
                var tsk = reg.SetValueFromModbus(datas, (UBUFP[2] * 256 + UBUFP[3]) * 2, modbusLen * 2);
                tsk.Wait();
                if(tsk.Result!= ModbusStatus.OK)
                    return backError((byte)(UBUFP[1] | 0x80), 0x03);
            }

            byte[] rebackArray = new byte[8];
            rebackArray[0] = ModbusId;
            rebackArray[1] = 0x10;
            rebackArray[2] = UBUFP[2];
            rebackArray[3] = UBUFP[3];
            rebackArray[4] = UBUFP[4];
            rebackArray[5] = UBUFP[5];
            var crc = CRCHelper.CRC16(rebackArray, 6);
            rebackArray[6] = crc[1];
            rebackArray[7] = crc[0];
            return rebackArray;

        }
        private byte[] modbus06(byte[] UBUFP, bool isModbusTcp)
        {
            ushort temp16 = (ushort)(UBUFP[2] * 256 + UBUFP[3] + 40001);
            if (temp16 > ReadWriteReg[ReadWriteReg.Count - 1].RegAddress)
                return backError((byte)(UBUFP[1] | 0x80), 0x02);
            byte[] datas = new byte[2];
            datas[0] = UBUFP[4];
            datas[1] = UBUFP[5];
            for (int i = 0; i < ReadWriteReg.Count; i++)
            {
                var reg = ReadWriteReg[i];
                var tsk = reg.SetValueFromModbus(datas, (UBUFP[2] * 256 + UBUFP[3]) * 2, 2);
                tsk.Wait();
                if (tsk.Result != ModbusStatus.OK)
                    return backError((byte)(UBUFP[1] | 0x80), 0x03);
            }
            return UBUFP;//06指令接收和返回数据相同
        }

        private byte[] backError(byte REFUNCTIONCODE, byte ERRORCODE)
        {
            byte[] errorArray = new byte[5];
            errorArray[0] = ModbusId;
            errorArray[1] = REFUNCTIONCODE;
            errorArray[2] = ERRORCODE;
            var crc = CRCHelper.CRC16(errorArray, 3);
            errorArray[3] = crc[1];
            errorArray[4] = crc[0];
            return errorArray;
        }
        private byte[] getDataFromReg(List<AtomModel> src, ushort startReg, ushort len)
        {
            byte[] array = new byte[len * 2];
            for (int i = 0; i < src.Count; i++)
            {
                var reg = src[i];
                if (startReg <= reg.RegAddress)
                {
                    int offset = (reg.RegAddress - startReg) * 2;//array数组插入偏移
                    var val = reg.GetValue();
                    int copyLen = val.Length;
                    if (array.Length - offset < val.Length)
                        copyLen = array.Length - offset;
                    if (copyLen <= 0) break;
                    Array.Copy(val, 0, array, offset, copyLen);
                }
            }
            return array;
        }
    }
}
