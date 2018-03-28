using System;
using System.Collections.Generic;
using System.Text;

namespace ClientServer
{
    public class dataPackage : IDisposable
    {
        private List<byte> bufferList;
        private byte[] readBuffer;
        private int readPos;
        private bool bufferUpdate;

        #region Public Access
        public dataPackage()
        {
            bufferList = new List<byte>();
            readPos = 0;
        }

        public dataPackage(byte[] data)
        {
            bufferList = new List<byte>();
            readPos = 0;

            this.write(data);
        }

        public void clear()
        {
            bufferList.Clear();
            readPos = 0;
        }

        public byte[] toArray()
        {
            return bufferList.ToArray();
        }

        private int getReadPos()
        {
            return readPos;
        }

        private int count()
        {
            return bufferList.Count;
        }

        private int length()
        {
            return count() - readPos;
        }
        #endregion

        #region Read / Write
        public bool write(object input)
        {
            if (input is byte b)
            {
                bufferList.Add(b);
            }
            else if (input is byte[] bs)
            {
                bufferList.AddRange(bs);
            }
            else if (input is int i)
            {
                bufferList.AddRange(BitConverter.GetBytes(i));
            }
            else if (input is float f)
            {
                bufferList.AddRange(BitConverter.GetBytes(f));

            }
            else if (input is string s)
            {
                bufferList.AddRange(BitConverter.GetBytes(s.Length));
                bufferList.AddRange(Encoding.ASCII.GetBytes(s));
            }
            else
            {
                return false;
            }

            bufferUpdate = true;
            return true;
        }

        public int readInteger(bool peek = true)
        {
            if (bufferList.Count > readPos)
            {
                if (bufferUpdate)
                {
                    readBuffer = bufferList.ToArray();
                    bufferUpdate = false;
                }

                int value = BitConverter.ToInt32(readBuffer, readPos);

                if (peek & bufferList.Count > readPos)
                {
                    readPos += 4;
                }

                return value;
            }
            else
            {
                throw new Exception("Buffer is past its Limit! - readInteger()");
            }
        }

        public float readFloat(bool peek = true)
        {
            if (bufferList.Count > readPos)
            {
                if (bufferUpdate)
                {
                    readBuffer = bufferList.ToArray();
                    bufferUpdate = false;
                }

                float value = BitConverter.ToSingle(readBuffer, readPos);

                if (peek & bufferList.Count > readPos)
                {
                    readPos += 4;
                }

                return value;
            }
            else
            {
                throw new Exception("Buffer is past its Limit! - readFloat()");
            }
        }

        public byte readByte(bool peek = true)
        {
            if (bufferList.Count > readPos)
            {
                if (bufferUpdate)
                {
                    readBuffer = bufferList.ToArray();
                    bufferUpdate = false;
                }

                byte value = readBuffer[readPos];

                if (peek & bufferList.Count > readPos)
                {
                    readPos += 1;
                }

                return value;
            }
            else
            {
                throw new Exception("Buffer is past its Limit! - readByte()");
            }
        }

        public byte[] readBytes(int length, bool peek = true)
        {
            if (bufferUpdate)
            {
                readBuffer = bufferList.ToArray();
                bufferUpdate = false;
            }

            byte[] value = bufferList.GetRange(readPos, length).ToArray();

            if (peek & bufferList.Count > readPos)
            {
                readPos += length;
            }

            return value;
        }

        public string readString(bool peek = true)
        {
            int length = readInteger(true);

            if (bufferUpdate)
            {
                readBuffer = bufferList.ToArray();
                bufferUpdate = false;
            }

            string value = Encoding.ASCII.GetString(readBuffer, readPos, length);

            if (peek & bufferList.Count > readPos)
            {
                readPos += length;
            }

            return value;

        }
        #endregion

        #region Disposable
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    bufferList.Clear();
                }

                readPos = 0;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
