//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class NewtonSafe : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal NewtonSafe(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(NewtonSafe obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(NewtonSafe obj) {
    if (obj != null) {
      if (!obj.swigCMemOwn)
        throw new global::System.ApplicationException("Cannot release ownership as memory is not owned");
      global::System.Runtime.InteropServices.HandleRef ptr = obj.swigCPtr;
      obj.swigCMemOwn = false;
      obj.Dispose();
      return ptr;
    } else {
      return new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
    }
  }

  ~NewtonSafe() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          NQuantLibcPINVOKE.delete_NewtonSafe(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public void setMaxEvaluations(uint evaluations) {
    NQuantLibcPINVOKE.NewtonSafe_setMaxEvaluations(swigCPtr, evaluations);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void setLowerBound(double lowerBound) {
    NQuantLibcPINVOKE.NewtonSafe_setLowerBound(swigCPtr, lowerBound);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void setUpperBound(double upperBound) {
    NQuantLibcPINVOKE.NewtonSafe_setUpperBound(swigCPtr, upperBound);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double solve(UnaryFunctionDelegate function, UnaryFunctionDelegate derivative, double xAccuracy, double guess, double step) {
    double ret = NQuantLibcPINVOKE.NewtonSafe_solve__SWIG_0(swigCPtr, UnaryFunctionDelegate.getCPtr(function), UnaryFunctionDelegate.getCPtr(derivative), xAccuracy, guess, step);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double solve(UnaryFunctionDelegate function, UnaryFunctionDelegate derivative, double xAccuracy, double guess, double xMin, double xMax) {
    double ret = NQuantLibcPINVOKE.NewtonSafe_solve__SWIG_1(swigCPtr, UnaryFunctionDelegate.getCPtr(function), UnaryFunctionDelegate.getCPtr(derivative), xAccuracy, guess, xMin, xMax);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public NewtonSafe() : this(NQuantLibcPINVOKE.new_NewtonSafe(), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
