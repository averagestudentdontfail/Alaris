//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BlackCalibrationHelper : CalibrationHelper {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal BlackCalibrationHelper(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.BlackCalibrationHelper_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BlackCalibrationHelper obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_BlackCalibrationHelper(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public void setPricingEngine(PricingEngine engine) {
    NQuantLibcPINVOKE.BlackCalibrationHelper_setPricingEngine(swigCPtr, PricingEngine.getCPtr(engine));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double marketValue() {
    double ret = NQuantLibcPINVOKE.BlackCalibrationHelper_marketValue(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double modelValue() {
    double ret = NQuantLibcPINVOKE.BlackCalibrationHelper_modelValue(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double impliedVolatility(double targetValue, double accuracy, uint maxEvaluations, double minVol, double maxVol) {
    double ret = NQuantLibcPINVOKE.BlackCalibrationHelper_impliedVolatility(swigCPtr, targetValue, accuracy, maxEvaluations, minVol, maxVol);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackPrice(double volatility) {
    double ret = NQuantLibcPINVOKE.BlackCalibrationHelper_blackPrice(swigCPtr, volatility);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public QuoteHandle volatility() {
    QuoteHandle ret = new QuoteHandle(NQuantLibcPINVOKE.BlackCalibrationHelper_volatility(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public VolatilityType volatilityType() {
    VolatilityType ret = (VolatilityType)NQuantLibcPINVOKE.BlackCalibrationHelper_volatilityType(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public new double calibrationError() {
    double ret = NQuantLibcPINVOKE.BlackCalibrationHelper_calibrationError(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public enum CalibrationErrorType {
    RelativePriceError,
    PriceError,
    ImpliedVolError
  }

}
