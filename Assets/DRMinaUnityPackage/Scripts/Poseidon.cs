using System;

namespace DRMinaUnityPackage.Scripts
{
    public static class Poseidon
    {
        private static int fullRounds;
        private static int partialRounds;
        private static bool hasInitialRoundConstant;
        private static int stateSize;
        private static int rate;
        private static int power;

        private static Field[][] roundConstants;
        private static Field[,] mds;

        static Poseidon()
        {
            fullRounds = Constants.FullRounds;
            partialRounds = Constants.PartialRounds;
            hasInitialRoundConstant = Constants.HasInitialRoundConstant;
            stateSize = Constants.StateSize;
            rate = Constants.Rate;
            power = Constants.Power;
            roundConstants = Constants.RoundConstants;
            mds = Constants.MdsMatrix;

            if (partialRounds != 0)
            {
                throw new ArgumentException("o1js don't support partial rounds");
            }
        }


        public static Field Hash(Field[] input)
        {
            var state = Update(InitialState(), input);
            return state[0];
        }

        private static Field[] InitialState()
        {
            var state = new Field[stateSize];
            for (int i = 0; i < stateSize; i++)
            {
                state[i] = new Field(0);
            }

            return state;
        }


        private static Field[] Update(Field[] state, Field[] input)
        {
            if (input.Length == 0)
            {
                Permutation(state);
                return state;
            }

            int n = (int)Math.Ceiling((double)input.Length / rate) * rate;
            var paddedInput = new Field[n];
            Array.Copy(input, paddedInput, input.Length);
            for (int i = input.Length; i < n; i++)
            {
                paddedInput[i] = new Field(0);
            }

            for (int blockIndex = 0; blockIndex < n; blockIndex += rate)
            {
                for (int i = 0; i < rate; i++)
                {
                    state[i] = state[i] + paddedInput[blockIndex + i];
                }

                Permutation(state);
            }

            return state;
        }


        private static void Permutation(Field[] state)
        {
            int offset = 0;
            if (hasInitialRoundConstant)
            {
                for (int i = 0; i < state.Length; i++)
                {
                    state[i] += roundConstants[0][i];
                }

                offset = 1;
            }

            for (int round = 0; round < fullRounds; round++)
            {
                for (int i = 0; i < state.Length; i++)
                {
                    state[i] = Field.Power(state[i], power);
                }

                var oldState = (Field[])state.Clone();
                for (int i = 0; i < state.Length; i++)
                {
                    Field[] mdsRow = new Field[state.Length];
                    for (int j = 0; j < state.Length; j++)
                    {
                        mdsRow[j] = mds[i, j];
                    }

                    state[i] = Field.Dot(mdsRow, oldState) + roundConstants[round + offset][i];
                }
            }
        }
    }
}

