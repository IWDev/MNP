
namespace MNP.Core
{
    /// <summary>
    /// Provides the methods needed for providing.
    /// </summary>
    /// <typeparam name="TDeserialisedType">The deserialised type eg: Queue</typeparam>
    /// <typeparam name="TSerialisedType">The serialised type eg: byte[]</typeparam>
    public interface ISerialiser<TDeserialisedType, TSerialisedType>
    {
        /// <summary>
        /// Serialises the source into the SerialisedType
        /// </summary>
        /// <param name="source">The item to be serialised</param>
        /// <returns>The serialised form of the source</returns>
        TSerialisedType Serialise(TDeserialisedType source);

        /// <summary>
        /// Deserialises the source into the DeserialisedType
        /// </summary>
        /// <param name="source">The item to be deserialised</param>
        /// <returns>The deserialised form of the source</returns>
        TDeserialisedType Deserialise(TSerialisedType source); 
    }
}
