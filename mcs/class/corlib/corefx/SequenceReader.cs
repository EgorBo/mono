// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.Runtime.CompilerServices;

namespace System.Buffers
{
#if !__MonoCS__
    public ref partial struct SequenceReader<T> where T : unmanaged, IEquatable<T>
    {
        private SequencePosition _currentPosition;
        private SequencePosition _nextPosition;
        private bool _moreData;
        private readonly long _length;

        /// <summary>
        /// Create a <see cref="SequenceReader{T}"/> over the given <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SequenceReader(ReadOnlySequence<T> sequence)
        {
            CurrentSpanIndex = 0;
            Consumed = 0;
            Sequence = sequence;
            _currentPosition = sequence.Start;
            _length = -1;

            sequence.GetFirstSpan(out ReadOnlySpan<T> first, out _nextPosition);
            CurrentSpan = first;
            _moreData = first.Length > 0;

            if (!_moreData && !sequence.IsSingleSegment)
            {
                _moreData = true;
                GetNextSpan();
            }
        }

        /// <summary>
        /// True when there is no more data in the <see cref="Sequence"/>.
        /// </summary>
        public readonly bool End => !_moreData;

        /// <summary>
        /// The underlying <see cref="ReadOnlySequence{T}"/> for the reader.
        /// </summary>
        public readonly ReadOnlySequence<T> Sequence { get; }

        /// <summary>
        /// The current position in the <see cref="Sequence"/>.
        /// </summary>
        public readonly SequencePosition Position
            => Sequence.GetPosition(CurrentSpanIndex, _currentPosition);

        /// <summary>
        /// The current segment in the <see cref="Sequence"/> as a span.
        /// </summary>
        public ReadOnlySpan<T> CurrentSpan { readonly get; private set; }

        /// <summary>
        /// The index in the <see cref="CurrentSpan"/>.
        /// </summary>
        public int CurrentSpanIndex { readonly get; private set; }

        /// <summary>
        /// The unread portion of the <see cref="CurrentSpan"/>.
        /// </summary>
        public readonly ReadOnlySpan<T> UnreadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CurrentSpan.Slice(CurrentSpanIndex);
        }

        /// <summary>
        /// The total number of <typeparamref name="T"/>'s processed by the reader.
        /// </summary>
        public long Consumed { readonly get; private set; }

        /// <summary>
        /// Remaining <typeparamref name="T"/>'s in the reader's <see cref="Sequence"/>.
        /// </summary>
        public readonly long Remaining => Length - Consumed;

        /// <summary>
        /// Count of <typeparamref name="T"/> in the reader's <see cref="Sequence"/>.
        /// </summary>
        public readonly long Length
        {
            get
            {
                if (_length < 0)
                {
                    unsafe {
                        fixed (long* lenPtr = &_length)
                             // Cast-away readonly to initialize lazy field
                            Volatile.Write(ref Unsafe.AsRef<long>(lenPtr), Sequence.Length);
                    }
                }
                return _length;
            }
        }

        /// <summary>
        /// Peeks at the next value without advancing the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(out T value)
        {
            if (_moreData)
            {
                value = CurrentSpan[CurrentSpanIndex];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Read the next value and advance the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out T value)
        {
            if (End)
            {
                value = default;
                return false;
            }

            value = CurrentSpan[CurrentSpanIndex];
            CurrentSpanIndex++;
            Consumed++;

            if (CurrentSpanIndex >= CurrentSpan.Length)
            {
                GetNextSpan();
            }

            return true;
        }

        /// <summary>
        /// Move the reader back the specified number of items.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if trying to rewind a negative amount or more than <see cref="Consumed"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(long count)
        {
            if ((ulong)count > (ulong)Consumed)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);
            }

            Consumed -= count;

            if (CurrentSpanIndex >= count)
            {
                CurrentSpanIndex -= (int)count;
                _moreData = true;
            }
            else
            {
                // Current segment doesn't have enough data, scan backward through segments
                RetreatToPreviousSpan(Consumed);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RetreatToPreviousSpan(long consumed)
        {
            ResetReader();
            Advance(consumed);
        }

        private void ResetReader()
        {
            CurrentSpanIndex = 0;
            Consumed = 0;
            _currentPosition = Sequence.Start;
            _nextPosition = _currentPosition;

            if (Sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<T> memory, advance: true))
            {
                _moreData = true;

                if (memory.Length == 0)
                {
                    CurrentSpan = default;
                    // No data in the first span, move to one with data
                    GetNextSpan();
                }
                else
                {
                    CurrentSpan = memory.Span;
                }
            }
            else
            {
                // No data in any spans and at end of sequence
                _moreData = false;
                CurrentSpan = default;
            }
        }

        /// <summary>
        /// Get the next segment with available data, if any.
        /// </summary>
        private void GetNextSpan()
        {
            if (!Sequence.IsSingleSegment)
            {
                SequencePosition previousNextPosition = _nextPosition;
                while (Sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<T> memory, advance: true))
                {
                    _currentPosition = previousNextPosition;
                    if (memory.Length > 0)
                    {
                        CurrentSpan = memory.Span;
                        CurrentSpanIndex = 0;
                        return;
                    }
                    else
                    {
                        CurrentSpan = default;
                        CurrentSpanIndex = 0;
                        previousNextPosition = _nextPosition;
                    }
                }
            }
            _moreData = false;
        }

        /// <summary>
        /// Move the reader ahead the specified number of items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(long count)
        {
            const long TooBigOrNegative = unchecked((long)0xFFFFFFFF80000000);
            if ((count & TooBigOrNegative) == 0 && CurrentSpan.Length - CurrentSpanIndex > (int)count)
            {
                CurrentSpanIndex += (int)count;
                Consumed += count;
            }
            else
            {
                // Can't satisfy from the current span
                AdvanceToNextSpan(count);
            }
        }

        /// <summary>
        /// Unchecked helper to avoid unnecessary checks where you know count is valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceCurrentSpan(long count)
        {
            Debug.Assert(count >= 0);

            Consumed += count;
            CurrentSpanIndex += (int)count;
            if (CurrentSpanIndex >= CurrentSpan.Length)
                GetNextSpan();
        }

        /// <summary>
        /// Only call this helper if you know that you are advancing in the current span
        /// with valid count and there is no need to fetch the next one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceWithinSpan(long count)
        {
            Debug.Assert(count >= 0);

            Consumed += count;
            CurrentSpanIndex += (int)count;

            Debug.Assert(CurrentSpanIndex < CurrentSpan.Length);
        }

        private void AdvanceToNextSpan(long count)
        {
            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);
            }

            Consumed += count;
            while (_moreData)
            {
                int remaining = CurrentSpan.Length - CurrentSpanIndex;

                if (remaining > count)
                {
                    CurrentSpanIndex += (int)count;
                    count = 0;
                    break;
                }

                // As there may not be any further segments we need to
                // push the current index to the end of the span.
                CurrentSpanIndex += remaining;
                count -= remaining;
                Debug.Assert(count >= 0);

                GetNextSpan();

                if (count == 0)
                {
                    break;
                }
            }

            if (count != 0)
            {
                // Not enough data left- adjust for where we actually ended and throw
                Consumed -= count;
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);
            }
        }

        /// <summary>
        /// Copies data from the current <see cref="Position"/> to the given <paramref name="destination"/> span if there
        /// is enough data to fill it.
        /// </summary>
        /// <remarks>
        /// This API is used to copy a fixed amount of data out of the sequence if possible. It does not advance
        /// the reader. To look ahead for a specific stream of data <see cref="IsNext(ReadOnlySpan{T}, bool)"/> can be used.
        /// </remarks>
        /// <param name="destination">Destination span to copy to.</param>
        /// <returns>True if there is enough data to completely fill the <paramref name="destination"/> span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryCopyTo(Span<T> destination)
        {
            // This API doesn't advance to facilitate conditional advancement based on the data returned.
            // We don't provide an advance option to allow easier utilizing of stack allocated destination spans.
            // (Because we can make this method readonly we can guarantee that we won't capture the span.)

            ReadOnlySpan<T> firstSpan = UnreadSpan;
            if (firstSpan.Length >= destination.Length)
            {
                firstSpan.Slice(0, destination.Length).CopyTo(destination);
                return true;
            }

            // Not enough in the current span to satisfy the request, fall through to the slow path
            return TryCopyMultisegment(destination);
        }

        internal readonly bool TryCopyMultisegment(Span<T> destination)
        {
            // If we don't have enough to fill the requested buffer, return false
            if (Remaining < destination.Length)
                return false;

            ReadOnlySpan<T> firstSpan = UnreadSpan;
            Debug.Assert(firstSpan.Length < destination.Length);
            firstSpan.CopyTo(destination);
            int copied = firstSpan.Length;

            SequencePosition next = _nextPosition;
            while (Sequence.TryGet(ref next, out ReadOnlyMemory<T> nextSegment, true))
            {
                if (nextSegment.Length > 0)
                {
                    ReadOnlySpan<T> nextSpan = nextSegment.Span;
                    int toCopy = Math.Min(nextSpan.Length, destination.Length - copied);
                    nextSpan.Slice(0, toCopy).CopyTo(destination.Slice(copied));
                    copied += toCopy;
                    if (copied >= destination.Length)
                    {
                        break;
                    }
                }
            }

            return true;
        }
    }
#else
    public ref partial struct SequenceReader<T> where T : System.IEquatable<T>
    {
        private object _dummy;
        private int _dummyPrimitive;
        public SequenceReader(System.Buffers.ReadOnlySequence<T> sequence) => throw new PlatformNotSupportedException();
        public long Consumed { get => throw new PlatformNotSupportedException(); }
        public System.ReadOnlySpan<T> CurrentSpan { get => throw new PlatformNotSupportedException(); }
        public int CurrentSpanIndex { get => throw new PlatformNotSupportedException(); }
        public bool End { get => throw new PlatformNotSupportedException(); }
        public long Length { get => throw new PlatformNotSupportedException(); }
        public System.SequencePosition Position { get => throw new PlatformNotSupportedException(); }
        public long Remaining { get => throw new PlatformNotSupportedException(); }
        public System.Buffers.ReadOnlySequence<T> Sequence { get => throw new PlatformNotSupportedException(); }
        public System.ReadOnlySpan<T> UnreadSpan { get => throw new PlatformNotSupportedException(); }
        public void Advance(long count) { }
        public long AdvancePast(T value) => throw new PlatformNotSupportedException();
        public long AdvancePastAny(System.ReadOnlySpan<T> values) => throw new PlatformNotSupportedException();
        public long AdvancePastAny(T value0, T value1) => throw new PlatformNotSupportedException();
        public long AdvancePastAny(T value0, T value1, T value2) => throw new PlatformNotSupportedException();
        public long AdvancePastAny(T value0, T value1, T value2, T value3) => throw new PlatformNotSupportedException();
        public bool IsNext(System.ReadOnlySpan<T> next, bool advancePast = false) => throw new PlatformNotSupportedException();
        public bool IsNext(T next, bool advancePast = false) => throw new PlatformNotSupportedException();
        public void Rewind(long count) { }
        public bool TryAdvanceTo(T delimiter, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryAdvanceToAny(System.ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryCopyTo(System.Span<T> destination) => throw new PlatformNotSupportedException();
        public bool TryPeek(out T value) => throw new PlatformNotSupportedException();
        public bool TryRead(out T value) => throw new PlatformNotSupportedException();
        public bool TryReadTo(out System.Buffers.ReadOnlySequence<T> sequence, System.ReadOnlySpan<T> delimiter, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadTo(out System.Buffers.ReadOnlySequence<T> sequence, T delimiter, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadTo(out System.Buffers.ReadOnlySequence<T> sequence, T delimiter, T delimiterEscape, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadTo(out System.ReadOnlySpan<T> span, T delimiter, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadTo(out System.ReadOnlySpan<T> span, T delimiter, T delimiterEscape, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadToAny(out System.Buffers.ReadOnlySequence<T> sequence, System.ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
        public bool TryReadToAny(out System.ReadOnlySpan<T> span, System.ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true) => throw new PlatformNotSupportedException();
    }
#endif
}