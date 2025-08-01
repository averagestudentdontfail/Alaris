//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class MonotonicCubicInterpolatedSmileSection : SmileSection {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal MonotonicCubicInterpolatedSmileSection(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.MonotonicCubicInterpolatedSmileSection_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(MonotonicCubicInterpolatedSmileSection obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_MonotonicCubicInterpolatedSmileSection(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, MonotonicCubic interpolator, DayCounter dc, VolatilityType type, double shift) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_0(expiryTime, DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc), (int)type, shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, MonotonicCubic interpolator, DayCounter dc, VolatilityType type) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_1(expiryTime, DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc), (int)type), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, MonotonicCubic interpolator, DayCounter dc) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_2(expiryTime, DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, MonotonicCubic interpolator) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_3(expiryTime, DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), MonotonicCubic.getCPtr(interpolator)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_4(expiryTime, DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, MonotonicCubic interpolator, DayCounter dc, VolatilityType type, double shift) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_5(expiryTime, DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc), (int)type, shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, MonotonicCubic interpolator, DayCounter dc, VolatilityType type) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_6(expiryTime, DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc), (int)type), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, MonotonicCubic interpolator, DayCounter dc) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_7(expiryTime, DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, MonotonicCubic.getCPtr(interpolator), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, MonotonicCubic interpolator) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_8(expiryTime, DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, MonotonicCubic.getCPtr(interpolator)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(double expiryTime, DoubleVector strikes, DoubleVector stdDevs, double atmLevel) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_9(expiryTime, DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate, VolatilityType type, double shift) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_10(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate), (int)type, shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate, VolatilityType type) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_11(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate), (int)type), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_12(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, DayCounter dc, MonotonicCubic interpolator) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_13(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel, DayCounter dc) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_14(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, QuoteHandleVector stdDevHandles, QuoteHandle atmLevel) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_15(Date.getCPtr(d), DoubleVector.getCPtr(strikes), QuoteHandleVector.getCPtr(stdDevHandles), QuoteHandle.getCPtr(atmLevel)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate, VolatilityType type, double shift) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_16(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate), (int)type, shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate, VolatilityType type) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_17(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate), (int)type), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, DayCounter dc, MonotonicCubic interpolator, Date referenceDate) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_18(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator), Date.getCPtr(referenceDate)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, DayCounter dc, MonotonicCubic interpolator) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_19(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, DayCounter.getCPtr(dc), MonotonicCubic.getCPtr(interpolator)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel, DayCounter dc) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_20(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel, DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MonotonicCubicInterpolatedSmileSection(Date d, DoubleVector strikes, DoubleVector stdDevs, double atmLevel) : this(NQuantLibcPINVOKE.new_MonotonicCubicInterpolatedSmileSection__SWIG_21(Date.getCPtr(d), DoubleVector.getCPtr(strikes), DoubleVector.getCPtr(stdDevs), atmLevel), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
