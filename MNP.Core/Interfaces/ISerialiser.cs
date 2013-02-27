
namespace MNP.Core
{
    /// <summary>
    /// Provides the methods needed for providing.
    /// </summary>
    /// <typeparam name="DeserialisedType">The deserialised type eg: Queue</typeparam>
    /// <typeparam name="SerialisedType">The serialised type eg: byte[]</typeparam>
    public interface ISerialiser<DeserialisedType, SerialisedType>
    {
        /// <summary>
        /// Serialises the source into the SerialisedType
        /// </summary>
        /// <param name="source">The item to be serialised</param>
        /// <returns>The serialised form of the source</returns>
        SerialisedType Serialise(DeserialisedType source);

        /// <summary>
        /// Deserialises the source into the DeserialisedType
        /// </summary>
        /// <param name="source">The item to be deserialised</param>
        /// <returns>The deserialised form of the source</returns>
        DeserialisedType Deserialise(SerialisedType source); 
    }
}
