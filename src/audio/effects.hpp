#pragma once

#include <vector>
#include <string>
#include <sstream>
#include <deque>
#include <unordered_map>
#include <algorithm>
#include <array>
#include <memory>
#include <cmath>

extern "C" {
    void error(const char* info);
}

#pragma region Structs

template <typename T>
struct FFISlice {
    const T* ptr;
    size_t len;
};

enum class FFIEffectsType : uint32_t {
    In = 0,
    Out,
    Split,
    Merge,
    Compression,
    Delay,
    Distortion,
    Gain,
    Gating,
    Reverb
};

struct FFIEffectNode {
    uint32_t x;
    uint32_t y;

    FFIEffectsType type_of;

    const char* id;

    FFISlice<const char*> inputs;
    FFISlice<const char*> outputs;
    FFISlice<const char*> options;
};

struct FFIEffectsConnection {
    const char* from_node_id;
    const char* from_port_id;
    const char* to_node_id;
    const char* to_port_id;
};

struct FFIEffects {
    FFISlice<FFIEffectNode> nodes;
    FFISlice<FFIEffectsConnection> connections;
};

struct AudioFrame {
    std::vector<float> channels;

    AudioFrame(int n = 2, float v = 0.0f) {
        channels.assign(n, v);
    }
};

struct InputConnection {
    int node_index;
    int output_index;
    int input_index;
};

class Block; // C++ being C++
struct RuntimeNode {
    std::string id;
    FFIEffectsType type;
    std::unique_ptr<Block> block;
    std::vector<InputConnection> inputs;
    std::vector<int> outputs;
    std::vector<std::string> options;
};

#pragma endregion
#pragma region Helpers

class Block {
public:
    virtual ~Block() = default;

    virtual size_t TailSamples() const { return 0; }
    virtual void Start(int num_channels) {}
    virtual void Process(float* frame, int num_channels) {}
};

class DelayLine {
public:
    std::vector<float> buffer;
    int size = 1;
    int index = 0;

    void Init(int samples) {
        size = std::max(1, samples);
        buffer.assign(size, 0.0f);
        index = 0;
    }

    float Read() const {
        return buffer[index];
    }

    void Write(float x) {
        buffer[index] = x;
        index = (index + 1) % size;
    }
};

class CombFilter {
public:
    DelayLine delay;
    float feedback = 0.7f;
    float damp = 0.5f;

    float filter_store = 0.0f;

    void Init(int delay_samples, float fb, float d) {
        delay.Init(delay_samples);
        feedback = fb;
        damp = d;
        filter_store = 0.0f;
    }

    float Process(float input) {
        float output = delay.Read();

        filter_store = output * (1.0f - damp) + filter_store * damp;
        delay.Write(input + filter_store * feedback);

        return output;
    }
};

class AllPassFilter {
public:
    DelayLine delay;
    float feedback = 0.5f;

    void Init(int delay_samples, float fb) {
        delay.Init(delay_samples);
        feedback = fb;
    }

    float Process(float input) {
        float buf = delay.Read();
        float output = -input + buf;
        delay.Write(input + buf * feedback);
        return output;
    }
};

#pragma endregion
#pragma region Classes

class DelayBlock : public Block {
public:
    float time_ms = 250.0f;
    float mix = 1.0f;
    int sample_rate = 44100;

    int delay_samples = 1;
    int num_channels = 2;

    std::vector<std::vector<float>> buffers;
    std::vector<int> write_idx;

    DelayBlock(float t_ms, int sr) : time_ms(t_ms), sample_rate(sr) {}

    size_t TailSamples() const override {
        return delay_samples;
    }

    void Start(int channels) override {
        num_channels = channels;
        delay_samples = std::max(1, int(time_ms * sample_rate / 1000.0f));

        buffers.assign(num_channels, std::vector<float>(delay_samples, 0.0f));
        write_idx.assign(num_channels, 0);
    }

    void Process(float* frame, int channels) override {
        for (int c = 0; c < channels; ++c) {
            float delayed = buffers[c][write_idx[c]];
            buffers[c][write_idx[c]] = frame[c];
            write_idx[c] = (write_idx[c] + 1) % delay_samples;

            frame[c] = frame[c] * (1.0f - mix) + delayed * mix;
        }
    }
};

class DistortionBlock : public Block {
public:
    float drive = 1.0f;

    DistortionBlock(float amount) {
        drive = 1.0f + amount * 20.0f;
    }

    void Process(float* frame, int num_channels) override {
        for (int c = 0; c < num_channels; ++c) {
            frame[c] = std::tanh(frame[c] * drive);
        }
    }
};

class CompressionBlock : public Block {
public:
    float threshold = 0.2f;
    float ratio = 4.0f;
    float env = 0.0f;

    float attack = 0.01f;
    float release = 0.1f;

    CompressionBlock(float a) : threshold(a) {}

    void Process(float* frame, int num_channels) override {
        for (int c = 0; c < num_channels; ++c) {
            float level = std::fabs(frame[c]);
            float coeff = level > env ? attack : release;
            env += coeff * (level - env);

            if (env <= threshold)
                return;

            float gain = threshold + (env - threshold) / ratio;
            gain /= env;

            frame[c] = frame[c] * gain;
        }
    }
};

class GainBlock : public Block {
public:
    float amount;

    GainBlock(float t) : amount(t) {}

    void Process(float* frame, int num_channels) override {
        for (int c = 0; c < num_channels; ++c) {
            frame[c] = frame[c] * amount;
        }
    }
};

class GatingBlock : public Block {
public:
    float threshold = 0.05f;
    float env = 0.0f;
    float attack = 0.01f;
    float release = 0.05f;

    GatingBlock(float t) : threshold(t) {}

    void Process(float* frame, int num_channels) override {
        for (int c = 0; c < num_channels; ++c) {
            float level = std::fabs(frame[c]);
            float coeff = level > env ? attack : release;
            env += coeff * (level - env);

            frame[c] = (env < threshold) ? 0.0f : frame[c];
        }
    }
};

class ReverbBlock : public Block {
public:
    int sample_rate = 44100;
    float room_size = 0.5f;
    float damp = 0.5f;
    float wet = 0.3f;
    float dry = 0.7f;

    int num_channels = 2;

    static constexpr int NUM_COMBS = 4;
    static constexpr int NUM_ALLPASS = 2;

    std::vector<std::array<CombFilter, NUM_COMBS>> combs;
    std::vector<std::array<AllPassFilter, NUM_ALLPASS>> allpass;

    ReverbBlock(float intensity, int sr) : sample_rate(sr) {
        room_size = std::max(-1.0f, std::min(1.0f, intensity));
    }

    size_t TailSamples() const override {
        return 1617 * 10;
    }

    void Start(int channels) override {
        num_channels = channels;

        combs.resize(num_channels);
        allpass.resize(num_channels);

        int comb_delays[NUM_COMBS] = {1557, 1617, 1491, 1422};
        int allpass_delays[NUM_ALLPASS] = {225, 556};

        for (int c = 0; c < num_channels; ++c) {
            for (int i = 0; i < NUM_COMBS; ++i) {
                int d = std::max(1, int(comb_delays[i] * (0.5f + room_size * 0.5f)));
                combs[c][i].Init(d, 0.7f + room_size * 0.2f, damp);
            }

            for (int i = 0; i < NUM_ALLPASS; ++i) {
                allpass[c][i].Init(allpass_delays[i], 0.5f);
            }
        }
    }

    void Process(float* frame, int channels) override {
        for (int c = 0; c < channels; ++c) {
            float input = frame[c];
            float sum = 0.0f;

            for (int i = 0; i < NUM_COMBS; ++i)
                sum += combs[c][i].Process(input);

            sum *= (1.0f / NUM_COMBS);

            for (int i = 0; i < NUM_ALLPASS; ++i)
                sum = allpass[c][i].Process(sum);

            frame[c] = input * dry + sum * wet;
        }
    }
};

#pragma endregion
#pragma region Manager

class BlocksManager {
public:
    void Initialize(const FFIEffects* effects, int sample_rate, int channels) {
        this->sample_rate = sample_rate;
        this->channels = channels;

        has_out_node = false;
        has_in_node = false;

        nodes.clear();
        id_to_index.clear();
        execution_order.clear();

        for (size_t i = 0; i < effects->nodes.len; ++i) {
            const auto& n = effects->nodes.ptr[i];

            RuntimeNode node;
            node.id = n.id;
            node.type = n.type_of;
            node.options = std::vector<std::string>(
                n.options.ptr,
                n.options.ptr + n.options.len
            );

            if (node.type == FFIEffectsType::Out) has_out_node = true;
            if (node.type == FFIEffectsType::In) has_in_node = true;

            switch (node.type) {
                case FFIEffectsType::Delay:
                    node.block = std::make_unique<DelayBlock>(
                        node.options.empty() ? 250.0f : std::stof(node.options[0]),
                        sample_rate
                    );
                    break;
                case FFIEffectsType::Gain:
                    node.block = std::make_unique<GainBlock>(
                        node.options.empty() ? 1.0f : std::stof(node.options[0])
                    );
                    break;
                case FFIEffectsType::Distortion:
                    node.block = std::make_unique<DistortionBlock>(
                        node.options.empty() ? 0.5f : std::stof(node.options[0])
                    );
                    break;
                case FFIEffectsType::Compression:
                    node.block = std::make_unique<CompressionBlock>(
                        node.options.empty() ? 0.2f : std::stof(node.options[0])
                    );
                    break;
                case FFIEffectsType::Gating:
                    node.block = std::make_unique<GatingBlock>(
                        node.options.empty() ? 0.05f : std::stof(node.options[0])
                    );
                    break;
                case FFIEffectsType::Reverb:
                    node.block = std::make_unique<ReverbBlock>(
                        node.options.empty() ? 0.5f : std::stof(node.options[0]),
                        sample_rate
                    );
                    break;
                default:
                    node.block = nullptr;
                    break;
            }

            id_to_index[node.id] = (int)nodes.size();
            nodes.push_back(std::move(node));
        }

        auto get_port_index = [](const FFISlice<const char*>& ports, const char* port_id) -> int {
            if (!port_id) return 0;
            std::string target_id(port_id);
            for (size_t i = 0; i < ports.len; ++i) {
                if (std::string(ports.ptr[i]) == target_id) return (int)i;
            }
            return 0;
        };

        for (size_t i = 0; i < effects->connections.len; ++i) {
            const auto& c = effects->connections.ptr[i];

            int from_idx = id_to_index[c.from_node_id];
            int to_idx = id_to_index[c.to_node_id];

            const auto& ffi_from = effects->nodes.ptr[from_idx];
            const auto& ffi_to = effects->nodes.ptr[to_idx];

            int out_port = get_port_index(ffi_from.outputs, c.from_port_id);
            int in_port = get_port_index(ffi_to.inputs, c.to_port_id);

            nodes[from_idx].outputs.push_back(to_idx);
            nodes[to_idx].inputs.push_back({ from_idx, out_port, in_port });
        }

        BuildOrder();

        for (auto& n : nodes)
            if (n.block)
                n.block->Start(channels);
    }

    bool Process(float* frame) {
        if (!has_in_node || !has_out_node) {
            for (int c = 0; c < channels; ++c) frame[c] = 0.0f;
            return false;
        }

        std::vector<AudioFrame> values(nodes.size(), AudioFrame(channels, 0.0f));
        bool wrote_output = false;

        for (int idx : execution_order) {
            auto& node = nodes[idx];
            AudioFrame in_buf(channels, 0.0f);

            if (node.type == FFIEffectsType::In) {
                for (int c = 0; c < channels; ++c) in_buf.channels[c] = frame[c];
                values[idx] = in_buf;
                continue;
            }

            if (!node.inputs.empty()) {
                if (node.type == FFIEffectsType::Merge) {
                    std::vector<int> counts(channels, 0);
                    for (auto& conn : node.inputs) {
                        auto& src = values[conn.node_index];
                        if (conn.input_index >= 0 && conn.input_index < channels) {
                            int src_c = (conn.output_index < channels) ? conn.output_index : 0;
                            in_buf.channels[conn.input_index] += src.channels[src_c];
                            counts[conn.input_index]++;
                        }
                    }
                    for (int c = 0; c < channels; ++c) {
                        if (counts[c] > 1) in_buf.channels[c] /= counts[c];
                    }
                } else {
                    for (auto& conn : node.inputs) {
                        auto& src = values[conn.node_index];
                        auto& src_node = nodes[conn.node_index];

                        if (src_node.type == FFIEffectsType::Split) {
                            int src_c = (conn.output_index < channels) ? conn.output_index : 0;
                            for (int c = 0; c < channels; ++c) in_buf.channels[c] += src.channels[src_c];
                        } else {
                            for (int c = 0; c < channels; ++c) in_buf.channels[c] += src.channels[c];
                        }
                    }
                    if (node.inputs.size() > 1) {
                        float inv = 1.0f / (float)node.inputs.size();
                        for (int c = 0; c < channels; ++c) in_buf.channels[c] *= inv;
                    }
                }
            }

            AudioFrame out_buf = in_buf;
            if (node.block) {
                node.block->Process(out_buf.channels.data(), channels);
            }

            if (node.type == FFIEffectsType::Out) {
                for (int c = 0; c < channels; ++c) frame[c] = out_buf.channels[c];
                wrote_output = true;
                continue;
            }

            values[idx] = out_buf;
        }

        if (!wrote_output) {
            error("Graph did not reach Out node");
            for (int c = 0; c < channels; ++c) frame[c] = 0.0f;
            return false;
        }
        return true;
    }

    size_t RequiredTailSamples() const {
        size_t max_tail = 0;
        for (const auto& n : nodes) {
            if (n.block && (n.type == FFIEffectsType::Delay || n.type == FFIEffectsType::Reverb))
                max_tail = std::max(max_tail, n.block->TailSamples());
        }
        return max_tail;
    }

private:
    int sample_rate = 44100;
    int channels = 2;
    bool has_out_node = false;
    bool has_in_node = false;

    std::vector<RuntimeNode> nodes;
    std::unordered_map<std::string, int> id_to_index;
    std::vector<int> execution_order;

    void BuildOrder() {
        std::vector<int> indeg(nodes.size(), 0);
        for (auto& n : nodes) {
            for (int o : n.outputs) indeg[o]++;
        }

        std::deque<int> q;
        for (int i = 0; i < (int)nodes.size(); ++i) {
            if (indeg[i] == 0) q.push_back(i);
        }

        while (!q.empty()) {
            int n = q.front();
            q.pop_front();
            execution_order.push_back(n);
            for (int o : nodes[n].outputs) {
                if (--indeg[o] == 0) q.push_back(o);
            }
        }

        if (execution_order.size() != nodes.size()) error("Cycle detected");
        if (!has_out_node) error("No Out node");
        if (!has_in_node) error("No In node");
    }
};