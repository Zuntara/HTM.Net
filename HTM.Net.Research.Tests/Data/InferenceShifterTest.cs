using System.Collections.Generic;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Data
{
    [TestClass]
    public class InferenceShifterTest
    {
        private void ShiftAndCheck(List<Map<InferenceElement, object>> inferences,
            List<Map<InferenceElement, object>> expectedOutput)
        {
            var inferenceShifter = new InferenceShifter();

            foreach (Tuple item in ArrayUtils.Zip(inferences, expectedOutput))
            {
                var inputResult = new ModelResult(inferences: (Map<InferenceElement, object>)item.Get(0));
                var outputResult = inferenceShifter.Shift(inputResult);
                Assert.AreEqual(outputResult.inferences, item.Get(1));
            }
        }

        [TestMethod]
        public void TestNoShift()
        {
            foreach (var element in new[] { InferenceElement.AnomalyScore, InferenceElement.Classification, InferenceElement.ClassConfidences })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object>{ { element, 1 }},
                    new Map<InferenceElement, object>{ { element, 2 }},
                    new Map<InferenceElement, object>{ { element, 3 }},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object>{ { element, 1 }},
                    new Map<InferenceElement, object>{ { element, 2 }},
                    new Map<InferenceElement, object>{ { element, 3 }},
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestNoShiftMultipleValues()
        {
            foreach (var element in new[] { InferenceElement.AnomalyScore, InferenceElement.Classification, InferenceElement.ClassConfidences })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new[] {1, 2, 3}}},
                    new Map<InferenceElement, object> {{element, new[] {4, 5, 6}}},
                    new Map<InferenceElement, object> {{element, new[] {5, 6, 7}}},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new[] {1, 2, 3}}},
                    new Map<InferenceElement, object> {{element, new[] {4, 5, 6}}},
                    new Map<InferenceElement, object> {{element, new[] {5, 6, 7}}},
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestSingleShift()
        {
            foreach (var element in new[] { InferenceElement.Prediction, InferenceElement.Encodings })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, 1}},
                    new Map<InferenceElement, object> {{element, 2}},
                    new Map<InferenceElement, object> {{element, 3}},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, null}},
                    new Map<InferenceElement, object> {{element, 1}},
                    new Map<InferenceElement, object> {{element, 2}},
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestSingleShiftMultipleValues()
        {
            foreach (var element in new[] { InferenceElement.Prediction, InferenceElement.Encodings })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new[] {1, 2, 3}}},
                    new Map<InferenceElement, object> {{element, new[] {4, 5, 6}}},
                    new Map<InferenceElement, object> {{element, new[] {5, 6, 7}}},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new object[] {null, null, null}}},
                    new Map<InferenceElement, object> {{element, new[] {1, 2, 3}}},
                    new Map<InferenceElement, object> {{element, new[] {4, 5, 6}}},
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestMultiStepShift()
        {
            foreach (var element in new[] { InferenceElement.MultiStepPredictions, InferenceElement.MultiStepBestPredictions })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 1 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 2 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 3 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 4 } } } },
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 1 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 2 } } } },
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestMultiStepShiftMultipleValues()
        {
            foreach (var element in new[] { InferenceElement.MultiStepPredictions, InferenceElement.MultiStepBestPredictions })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {1, 11}}}}},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {2, 12}}}}},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {3, 13}}}}},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {4, 14}}}}},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, new[] { 1, 11 } } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, new[] { 2, 12 } } } } },
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestDifferentMultiStepsShift()
        {
            foreach (var element in new[] { InferenceElement.MultiStepPredictions, InferenceElement.MultiStepBestPredictions })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 1 }, { 3, 5 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 2 }, { 3, 6 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 3 }, { 3, 7 } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 4 }, { 3, 8 } } } },
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null }, { 3, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null }, { 3, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 1 }, { 3, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, 2 }, { 3, 5 } } } },
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }

        [TestMethod]
        public void TestDifferentMultiStepsShiftMultipleValues()
        {
            foreach (var element in new[] { InferenceElement.MultiStepPredictions, InferenceElement.MultiStepBestPredictions })
            {
                List<Map<InferenceElement, object>> inferences = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {1, 11}}, { 3, new[] { 5, 15 } } } }},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {2, 12}}, { 3, new[] { 6, 16 } } } }},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {3, 13}}, { 3, new[] { 7, 17 } } } }},
                    new Map<InferenceElement, object> {{element, new Map<object, object> {{2, new[] {4, 14}}, { 3, new[] { 8, 18 } } } }},
                };

                List<Map<InferenceElement, object>> expectedOutput = new List<Map<InferenceElement, object>>
                {
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null }, { 3, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, null }, { 3, null } } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, new[] { 1, 11 } }, {3, null} } } },
                    new Map<InferenceElement, object> {{element, new Map<object, object> { { 2, new[] { 2, 12 } }, {3, new[] { 5, 15 } } } } },
                };

                ShiftAndCheck(inferences, expectedOutput);
            }
        }
    }
}