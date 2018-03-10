using System;
using System.Collections.Generic;
using System.Text;

namespace ClientApp
{
    public static class globalVar
    {
        public static int SERVER_PORT = 5555;
        public static int SERVER_MAX_PENDING_CONNECTIONS = 10;

        public static int BUFFER_BYTE = 1024;
        public static int DATA_SIZE_INFO_SIZE = 4;
        public static int MAX_CLIENTS = 1000;
    }
}
