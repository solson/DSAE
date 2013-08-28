//---------------------------------------------------------------------------
// Includes
//---------------------------------------------------------------------------


// Header
#include "KinectTable.h"

// Project
#include "DataTypes/DataTypes.h"
#include "LocalClient.h"
#include "RemoteClient.h"

// Standard
#include <exception>
#include <errno.h>
#include <assert.h>



//---------------------------------------------------------------------------
// Namespaces
//---------------------------------------------------------------------------


//!! Should handle turning on and off the data processor if none is connected.
namespace KinectTable
{

//---------------------------------------------------------------------------
// Data Type Declarations
//---------------------------------------------------------------------------

//---------------------------------------------------------------------------
// Globals
//---------------------------------------------------------------------------

// This tweaks how low arms can be detected. If arms don't work close to the table, increase this.
// Don't set it too high or the table itself will be detected as arm blobs.
int tableDepthTweak = 25;

//---------------------------------------------------------------------------
// Private Method Declarations
//---------------------------------------------------------------------------


//---------------------------------------------------------------------------
// Public Methods
//---------------------------------------------------------------------------


Client* ConnectLocal(const SessionParameters& sessionParameters)
{
	return new LocalClient(sessionParameters);
}

Client* ConnectLocal(const SessionParameters& sessionParameters, const char* localAddress)
{
	return new LocalClient(sessionParameters, localAddress);
}


Client* ConnectRemote(const SessionParameters& sessionParameters, const char* remoteAddress)
{
	return new RemoteClient(sessionParameters, remoteAddress);
}

void Disconnect(Client* client)
{
	delete client;
	return;
}


}