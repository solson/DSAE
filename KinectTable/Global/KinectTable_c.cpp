//---------------------------------------------------------------------------
// Includes
//---------------------------------------------------------------------------

// Header
#include "KinectTable_c.h"

// Project
#include "KinectTable/KinectTable.h"



//---------------------------------------------------------------------------
// Public Methods
//---------------------------------------------------------------------------

DLLEXPORT int GetKinectTableDepthTweak()
{
	return KinectTable::tableDepthTweak;
}

DLLEXPORT void SetKinectTableDepthTweak(int x)
{
	KinectTable::tableDepthTweak = x;
}

DLLEXPORT KinectTable::Client* KinectTableConnectLocalPrivate(const SessionParameters* sessionParameters)
{
	return KinectTable::ConnectLocal(*sessionParameters);
}
	

DLLEXPORT KinectTable::Client* KinectTableConnectLocalPublic(const SessionParameters* sessionParameters, const char* localAddress)
{
	return KinectTable::ConnectLocal(*sessionParameters, localAddress);
}


DLLEXPORT KinectTable::Client* KinectTableConnectRemote(const SessionParameters* sessionParameters, const char* remoteAddress)
{
	return KinectTable::ConnectRemote(*sessionParameters, remoteAddress);
}

DLLEXPORT void KinectTableDisconnect(KinectTable::Client* client)
{
	KinectTable::Disconnect(client);
	return;
}