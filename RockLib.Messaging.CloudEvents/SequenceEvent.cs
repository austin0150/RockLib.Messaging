﻿namespace RockLib.Messaging.CloudEvents
{
    /// <summary>
    /// This extension defines two attributes that can be included within a CloudEvent to describe the
    /// position of an event in the ordered sequence of events produced by a unique event source.
    /// 
    /// <para>The <c>sequence</c> attribute represents the value of this event's order in the stream of
    /// events. The exact value and meaning of this attribute is defined by the <c>sequencetype</c>
    /// attribute. If the <c>sequencetype</c> is missing, or not defined in this specification, event
    /// consumers will need to have some out-of-band communication with the event producer to understand
    /// how to interpret the value of the attribute.</para>
    /// </summary>
    public class SequenceEvent : CloudEvent
    {
        /// <summary>The name of the <see cref="Sequence"/> attribute.</summary>
        public const string SequenceAttribute = "sequence";
        /// <summary>The name of the <see cref="SequenceType"/> attribute.</summary>
        public const string SequenceTypeAttribute = "sequencetype";

        /// <summary>
        /// REQUIRED. Value expressing the relative order of the event. This enables interpretation of
        /// data supercedence.
        /// </summary>
        public string Sequence { get; set; }

        /// <summary>
        /// Specifies the semantics of the sequence attribute. See <see cref="SequenceTypes"/> for known
        /// values of this attribute.
        /// </summary>
        public string SequenceType { get; set; }

        /// <summary>
        /// Creates a <see cref="SenderMessage"/> with headers mapped from the attributes of this sequence event.
        /// </summary>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map SequenceEvent attributes to <see cref="SenderMessage"/>
        /// headers. If <see langword="null"/>, then <see cref="CloudEvent.DefaultProtocolBinding"/> is used
        /// instead.
        /// </param>
        /// <returns>The mapped <see cref="SenderMessage"/>.</returns>
        public override SenderMessage ToSenderMessage(IProtocolBinding protocolBinding = null)
        {
            var senderMessage = base.ToSenderMessage(protocolBinding);

            if (Sequence != null)
                senderMessage.Headers[protocolBinding.GetHeaderName(SequenceAttribute)] = Sequence;

            if (SequenceType != null)
                senderMessage.Headers[protocolBinding.GetHeaderName(SequenceTypeAttribute)] = SequenceType;

            return senderMessage;
        }

        /// <summary>
        /// Ensures that the attributes for the sequence event are present.
        /// </summary>
        /// <param name="senderMessage">The <see cref="SenderMessage"/> to validate.</param>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map CloudEvent attributes to <see cref="SenderMessage"/>
        /// headers. If <see langword="null"/>, then <see cref="CloudEvent.DefaultProtocolBinding"/> is used
        /// instead.
        /// </param>
        public static void Validate(SenderMessage senderMessage, IProtocolBinding protocolBinding = null)
        {
            ValidateCore(senderMessage, ref protocolBinding);

            var sequenceHeader = protocolBinding.GetHeaderName(SequenceAttribute);
            if (!TryGetHeaderValue<string>(senderMessage, sequenceHeader, out _))
                throw new CloudEventValidationException($"The '{sequenceHeader}' header is missing from the SenderMessage.");
        }

        /// <summary>
        /// Creates an instance of <see cref="SequenceEvent"/> and initializes its sequence event
        /// attributes according to the payload and headers of the <paramref name="receiverMessage"/>.
        /// </summary>
        /// <param name="receiverMessage">
        /// The <see cref="IReceiverMessage"/> with headers that map to sequence event attributes.
        /// </param>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map <see cref="IReceiverMessage"/> headers to
        /// CloudEvent attributes. If <see langword="null"/>, then <see cref="CloudEvent.DefaultProtocolBinding"/>
        /// is used instead.
        /// </param>
        /// <returns>
        /// A new instance of <see cref="SequenceEvent"/> with its sequence event attributes set.
        /// </returns>
        public static SequenceEvent Create(IReceiverMessage receiverMessage, IProtocolBinding protocolBinding = null)
        {
            var cloudEvent = CreateCore<SequenceEvent>(receiverMessage, ref protocolBinding);

            var sequenceHeader = protocolBinding.GetHeaderName(SequenceAttribute);
            if (receiverMessage.Headers.TryGetValue(sequenceHeader, out string sequence))
            {
                cloudEvent.Sequence = sequence;
                cloudEvent.AdditionalAttributes.Remove(sequenceHeader);
            }

            var sequenceTypeHeader = protocolBinding.GetHeaderName(SequenceTypeAttribute);
            if (receiverMessage.Headers.TryGetValue(sequenceTypeHeader, out string sequenceType))
            {
                cloudEvent.SequenceType = sequenceType;
                cloudEvent.AdditionalAttributes.Remove(sequenceTypeHeader);
            }

            return cloudEvent;
        }
    }
}
