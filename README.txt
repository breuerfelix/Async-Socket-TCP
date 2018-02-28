	HOW TO USE clientTCP

        //Setup Client
        clientTCP client = new clientTCP(); 
        client.setupClient();
        client.connect();

        //connect the Delegate Function
        client.handleFunctions.Add("message", handleStringMessage);
        make a function called handleStringMessage(byte[] data);
            
        //Send Data to the Server
        dataPackage pack = new dataPackage();
        pack.write("message");
        pack.write("Hello, this is a Message. - CLIENT");
        client.sendData(pack.toArray());
        pack.Dispose();

        HOW TO USE serverTCP

        //Setup Server
        serverTCP server = new serverTCP(); 
        server.setupServer();

        //connect the Delegate Function
        server.handleFunctions.Add("message", handleStringMessage);
        make a function called handleStringMessage(int clientID, byte[] data);

        //Send Data to the Client
        dataPackage pack = new dataPackage();
        pack.write("message");
        pack.write("Hello, this is a Message. - CLIENT");
        server.sendData(clientID, pack.toArray());
        pack.Dispose();