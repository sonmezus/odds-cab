#ifndef ODDSCAB_CURVED_WORLD_INCLUDED
#define ODDSCAB_CURVED_WORLD_INCLUDED

// Curved road (CurveBender.cs): past a straight stretch ahead of the taxi, every vertex is pushed sideways and up by
// k * d^2, d = distance beyond that stretch. The street, its passengers and landmarks bend into the route's real turns
// (and rise on the Galata hill) while the game itself keeps driving a straight line.
float4 _CurveOrigin;   // xyz: taxi position, w: straight distance ahead before the bend starts (m)
float4 _CurveAmount;   // x: sideways k (+ = bends right), y: rise k (+ = climbs)

// Past the end of the view (112 m) the parabola carries on as its tangent, so street pieces streamed far ahead are not
// flung sideways across the screen
#define CURVE_FULL_DISTANCE 90.0

float3 CurveWorldPosition(float3 positionWS)
{
    float d = max(0.0, positionWS.z - _CurveOrigin.z - _CurveOrigin.w);
    float dn = min(d, CURVE_FULL_DISTANCE);
    float bend = dn * dn + 2.0 * CURVE_FULL_DISTANCE * (d - dn);
    positionWS.x += _CurveAmount.x * bend;
    positionWS.y += _CurveAmount.y * bend;
    return positionWS;
}

float3 CurveObjectPosition(float3 positionOS)
{
    return TransformWorldToObject(CurveWorldPosition(TransformObjectToWorld(positionOS)));
}

#endif
