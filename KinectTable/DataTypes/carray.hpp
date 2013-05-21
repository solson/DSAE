//---------------------------------------------------------------------------
// Module Definition
//---------------------------------------------------------------------------

#define MOD_DATA_TYPES

//---------------------------------------------------------------------------
// Includes
//---------------------------------------------------------------------------


// Project


// Standard C++


// Standard C



//---------------------------------------------------------------------------
// Namespaces
//---------------------------------------------------------------------------




namespace DataTypes
{

//---------------------------------------------------------------------------
// Globals
//---------------------------------------------------------------------------


//---------------------------------------------------------------------------
// Private Method Declarations
//---------------------------------------------------------------------------


//---------------------------------------------------------------------------
// Public Methods
//---------------------------------------------------------------------------


template<typename T, size_t S>
carray<T,S>::carray()
	: data((ArrayType&)*(new T[S])), size(0)
{
	return;
}



template<typename T, size_t S>
carray<T,S>::~carray()
{
	delete[] (T*)data;
}


template<typename T, size_t S>
void carray<T,S>::CopyTo(carray<T,S>& dst) const
{
	memcpy(dst.data, data, sizeof(dst.data));
	dst.size = size;
	return;
}



//---------------------------------------------------------------------------
// Private Methods
//---------------------------------------------------------------------------
		


}