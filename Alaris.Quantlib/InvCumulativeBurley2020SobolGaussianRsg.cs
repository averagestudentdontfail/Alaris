//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class InvCumulativeBurley2020SobolGaussianRsg : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal InvCumulativeBurley2020SobolGaussianRsg(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(InvCumulativeBurley2020SobolGaussianRsg obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(InvCumulativeBurley2020SobolGaussianRsg obj) {
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

  ~InvCumulativeBurley2020SobolGaussianRsg() {
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
          NQuantLibcPINVOKE.delete_InvCumulativeBurley2020SobolGaussianRsg(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public InvCumulativeBurley2020SobolGaussianRsg(Burley2020SobolRsg uniformSequenceGenerator) : this(NQuantLibcPINVOKE.new_InvCumulativeBurley2020SobolGaussianRsg__SWIG_0(Burley2020SobolRsg.getCPtr(uniformSequenceGenerator)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public InvCumulativeBurley2020SobolGaussianRsg(Burley2020SobolRsg uniformSequenceGenerator, InverseCumulativeNormal inverseCumulative) : this(NQuantLibcPINVOKE.new_InvCumulativeBurley2020SobolGaussianRsg__SWIG_1(Burley2020SobolRsg.getCPtr(uniformSequenceGenerator), InverseCumulativeNormal.getCPtr(inverseCumulative)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SampleRealVector nextSequence() {
    SampleRealVector ret = new SampleRealVector(NQuantLibcPINVOKE.InvCumulativeBurley2020SobolGaussianRsg_nextSequence(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public uint dimension() {
    uint ret = NQuantLibcPINVOKE.InvCumulativeBurley2020SobolGaussianRsg_dimension(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
